using S7.Connection;
using S7.Protocol;
using S7.Serialization;

using Serilog;
using Serilog.Core;

using Shared.Entities;

namespace S7.Sync;

public sealed class PlcTransactionExecutor
{
	private readonly ArrayCodec _arrayCodec;
	private readonly ExecutionStateCodec _executionCodec;
	private readonly PlcProtocolLayout _layout;
	private readonly Logger _logger;
	private readonly ManagingAreaCodec _managingCodec;
	private readonly PlcProtocolSettings _protocolSettings;
	private readonly RecipeConverter _converter;
	private readonly PlcTransport _transport;

	public PlcTransactionExecutor(
		PlcTransport transport,
		RecipeConverter converter,
		PlcConfiguration plcConfiguration,
		Logger logger)
	{
		_transport = transport;
		_converter = converter;
		_logger = logger;
		_layout = plcConfiguration.Layout;
		_protocolSettings = plcConfiguration.ProtocolSettings;
		_arrayCodec = new ArrayCodec(_layout.IntDb, _layout.FloatDb, _layout.StringDb);
		_executionCodec = new ExecutionStateCodec(_layout.ExecutionDb);
		_managingCodec = new ManagingAreaCodec(_layout.ManagingDb);
	}

	public async Task<bool> IsRecipeActiveAsync(CancellationToken ct = default)
	{
		EnsureConnected();

		var bytes = await _transport.ReadBytesAsync(
			_layout.ExecutionDb.DbNumber,
			0,
			_layout.ExecutionDb.TotalSize,
			ct);

		var state = _executionCodec.Decode(bytes);

		return state.RecipeActive;
	}

	public async Task<PlcExecutionState> ReadExecutionStateAsync(CancellationToken ct = default)
	{
		EnsureConnected();

		var bytes = await _transport.ReadBytesAsync(
			_layout.ExecutionDb.DbNumber,
			0,
			_layout.ExecutionDb.TotalSize,
			ct);

		return _executionCodec.Decode(bytes);
	}

	public async Task<PlcRecipeData> ReadRecipeDataAsync(CancellationToken ct = default)
	{
		EnsureConnected();

		var intHeaderBytes = await _transport.ReadBytesAsync(
			_layout.IntDb.DbNumber,
			0,
			_layout.IntDb.DataStartOffset,
			ct);
		var intCount = _arrayCodec.ReadArrayCurrentSize(intHeaderBytes, _layout.IntDb);

		var floatHeaderBytes = await _transport.ReadBytesAsync(
			_layout.FloatDb.DbNumber,
			0,
			_layout.FloatDb.DataStartOffset,
			ct);
		var floatCount = _arrayCodec.ReadArrayCurrentSize(floatHeaderBytes, _layout.FloatDb);

		var stringHeaderBytes = await _transport.ReadBytesAsync(
			_layout.StringDb.DbNumber,
			0,
			_layout.StringDb.DataStartOffset,
			ct);
		var stringCount = _arrayCodec.ReadArrayCurrentSize(stringHeaderBytes, _layout.StringDb);

		var intDataSize = _layout.IntDb.DataStartOffset + intCount * ProtocolConstants.IntElementSize;
		var floatDataSize = _layout.FloatDb.DataStartOffset + floatCount * ProtocolConstants.FloatElementSize;
		var stringDataSize = _layout.StringDb.DataStartOffset + stringCount * ProtocolConstants.WStringElementSize;

		var intData = await _transport.ReadBytesAsync(_layout.IntDb.DbNumber, 0, intDataSize, ct);
		var floatData = await _transport.ReadBytesAsync(_layout.FloatDb.DbNumber, 0, floatDataSize, ct);
		var stringData = await _transport.ReadBytesAsync(_layout.StringDb.DbNumber, 0, stringDataSize, ct);

		var intValues = _arrayCodec.DecodeIntArray(intData, intCount);
		var floatValues = _arrayCodec.DecodeFloatArray(floatData, floatCount);
		var stringValues = _arrayCodec.DecodeStringArray(stringData, stringCount);

		var stepCount = intCount > 0 ? intCount : 0;

		return new PlcRecipeData(intValues, floatValues, stringValues, stepCount);
	}

	public async Task WriteRecipeWithRetryAsync(Core.Entities.Recipe recipe, CancellationToken ct = default)
	{
		var recipeData = _converter.FromRecipe(recipe);

		for (var attempt = 1; attempt <= _protocolSettings.MaxRetryAttempts; attempt++)
		{
			try
			{
				await WriteRecipeDataAsync(recipeData, ct);
				_logger.Information(
					"Recipe synced to PLC successfully ({StepCount} steps)",
					recipe.StepCount);

				return;
			}
			catch (PlcSyncException ex) when (attempt < _protocolSettings.MaxRetryAttempts && IsRetryableError(ex.ErrorCode))
			{
				_logger.Warning(
					"Sync attempt {Attempt} failed with {Error}, retrying...",
					attempt,
					ex.ErrorCode);
			}
		}

		throw new PlcSyncException(
			$"Failed to sync recipe after {_protocolSettings.MaxRetryAttempts} attempts",
			PlcSyncError.ChecksumMismatchMultiple);
	}

	private async Task WriteRecipeDataAsync(PlcRecipeData data, CancellationToken ct)
	{
		EnsureConnected();

		var managingState = await ReadManagingAreaAsync(ct);
		if (managingState.PlcStatus == PlcSyncStatus.Busy)
		{
			throw new PlcBusyException("PLC is busy processing another transaction");
		}

		var transactionId = (uint)Random.Shared.Next();

		var checksumInt = data.IntValues.Length > 0
			? ChecksumCalculator.ComputeCrc32(data.IntValues)
			: 0u;
		var checksumFloat = data.FloatValues.Length > 0
			? ChecksumCalculator.ComputeCrc32(data.FloatValues)
			: 0u;
		var checksumString = data.StringValues.Length > 0
			? ChecksumCalculator.ComputeStringArrayCrc32(data.StringValues, ProtocolConstants.WStringMaxChars)
			: 0u;

		var pcData = new ManagingAreaPcData(
			Status: PcStatus.Writing,
			TransactionId: transactionId,
			ChecksumInt: checksumInt,
			ChecksumFloat: checksumFloat,
			ChecksumString: checksumString,
			RecipeLines: (uint)data.StepCount);

		await WriteManagingAreaAsync(pcData, ct);

		var intBytes = _arrayCodec.EncodeIntArray(data.IntValues);
		var floatBytes = _arrayCodec.EncodeFloatArray(data.FloatValues);
		var stringBytes = _arrayCodec.EncodeStringArray(data.StringValues);

		await _transport.WriteBytesAsync(_layout.IntDb.DbNumber, 0, intBytes, ct);
		await _transport.WriteBytesAsync(_layout.FloatDb.DbNumber, 0, floatBytes, ct);
		await _transport.WriteBytesAsync(_layout.StringDb.DbNumber, 0, stringBytes, ct);

		var commitData = pcData with { Status = PcStatus.CommitRequest };
		await WriteManagingAreaAsync(commitData, ct);

		await WaitForPlcConfirmationAsync(transactionId, ct);

		var idleData = pcData with { Status = PcStatus.Idle };
		await WriteManagingAreaAsync(idleData, ct);
	}

	private async Task<ManagingAreaState> ReadManagingAreaAsync(CancellationToken ct)
	{
		var bytes = await _transport.ReadBytesAsync(
			_layout.ManagingDb.DbNumber,
			0,
			_layout.ManagingDb.TotalSize,
			ct);

		return _managingCodec.Decode(bytes);
	}

	private async Task WriteManagingAreaAsync(ManagingAreaPcData data, CancellationToken ct)
	{
		var bytes = _managingCodec.EncodePcData(data);
		await _transport.WriteBytesAsync(_layout.ManagingDb.DbNumber, 0, bytes, ct);
	}

	private async Task WaitForPlcConfirmationAsync(uint transactionId, CancellationToken ct)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(_protocolSettings.CommitTimeoutMs);

		while (DateTime.UtcNow < deadline)
		{
			ct.ThrowIfCancellationRequested();

			var state = await ReadManagingAreaAsync(ct);

			switch (state.PlcStatus)
			{
				case PlcSyncStatus.Success when state.PlcStoredId == transactionId:
					return;

				case PlcSyncStatus.Error:
					throw new PlcSyncException(
						$"PLC reported sync error: {state.PlcError}",
						state.PlcError);

				case PlcSyncStatus.Idle:
				case PlcSyncStatus.CrcComputing:
				case PlcSyncStatus.Busy:
				default:
					break;
			}
			await Task.Delay(_protocolSettings.PollingIntervalMs, ct);
		}

		throw new PlcSyncTimeoutException("Timeout waiting for PLC confirmation");
	}

	private void EnsureConnected()
	{
		if (!_transport.IsConnected)
		{
			throw new PlcNotConnectedException("Not connected to PLC");
		}
	}

	private static bool IsRetryableError(PlcSyncError error)
	{
		return error is PlcSyncError.ChecksumMismatchInt
			or PlcSyncError.ChecksumMismatchFloat
			or PlcSyncError.ChecksumMismatchString
			or PlcSyncError.ChecksumMismatchMultiple
			or PlcSyncError.Timeout;
	}
}
