using System.Buffers.Binary;

using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.Sync;

internal sealed class PlcTransactionExecutor
{
	private readonly ArrayCodec _arrayCodec;
	private readonly RecipeConverter _converter;
	private readonly ExecutionStateCodec _executionCodec;
	private readonly PlcProtocolLayout _layout;
	private readonly ILogger<PlcTransactionExecutor> _logger;
	private readonly ManagingAreaCodec _managingCodec;
	private readonly PlcProtocolSettings _protocolSettings;
	private readonly IS7Transport _transport;

	// Written by the execution poll, the keep-alive and UI-started reads on pool threads; read on the UI thread.
	private long _lastExchangeUtcTicks;

	public PlcTransactionExecutor(
		IS7Transport transport,
		RecipeConverter converter,
		ArrayCodec arrayCodec,
		PlcConfiguration plcConfiguration,
		ILogger<PlcTransactionExecutor> logger)
	{
		_transport = transport;
		_converter = converter;
		_arrayCodec = arrayCodec;
		_layout = plcConfiguration.Layout;
		_protocolSettings = plcConfiguration.ProtocolSettings;
		_logger = logger;
		_executionCodec = new ExecutionStateCodec(_layout.ExecutionDb);
		_managingCodec = new ManagingAreaCodec(_layout.ManagingDb);
	}

	public DateTimeOffset? LastExchangeTime
	{
		get
		{
			var ticks = Interlocked.Read(ref _lastExchangeUtcTicks);
			return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
		}
	}

	/// <summary>Throws on a failed read, as the transport does, so the caller treats it as a lost link.</summary>
	public async Task KeepAliveReadAsync(CancellationToken ct)
	{
		await ReadBytesAsync(_layout.ManagingDb.DbNumber, 0, 1, ct);
	}

	public async Task<Result<bool>> IsRecipeActiveAsync(CancellationToken ct = default)
	{
		var result = await ReadExecutionStateAsync(ct);
		return result.Map(info => info.RecipeActive);
	}

	public Task<Result<PlcExecutionInfo>> ReadExecutionStateAsync(CancellationToken ct = default)
	{
		return ReadAndDecodeAsync(
			_layout.ExecutionDb.DbNumber,
			_layout.ExecutionDb.TotalSize,
			_executionCodec.Decode,
			ct);
	}

	public async Task<Result> WriteRecipeWithRetryAsync(Recipe recipe, CancellationToken ct = default)
	{
		if (!_transport.IsConnected)
		{
			return Result.Fail(new NotConnectedError());
		}

		var dataResult = _converter.FromRecipe(recipe);
		if (dataResult.IsFailed)
		{
			return dataResult.ToResult();
		}

		var recipeData = dataResult.Value;

		for (var attempt = 1; attempt <= _protocolSettings.MaxRetryAttempts; attempt++)
		{
			var writeResult = await WriteRecipeDataAsync(recipeData, ct);
			if (writeResult.IsFailed)
			{
				return writeResult;
			}

			var verifyResult = await VerifyWriteAsync(recipeData, ct);
			if (verifyResult.IsFailed)
			{
				return verifyResult.ToResult();
			}

			if (verifyResult.Value)
			{
				_logger.LogInformation(
					"Recipe synced to PLC successfully ({StepCount} steps, attempt {Attempt})",
					recipe.StepCount,
					attempt);

				return Result.Ok();
			}

			_logger.LogWarning(
				"Write verification failed on attempt {Attempt} of {MaxAttempts}",
				attempt,
				_protocolSettings.MaxRetryAttempts);
		}

		return Result.Fail(
			new WriteVerificationFailedError(_protocolSettings.MaxRetryAttempts));
	}

	public Task<Result<PlcManagingAreaState>> ReadManagingAreaAsync(CancellationToken ct = default)
	{
		return ReadAndDecodeAsync(
			_layout.ManagingDb.DbNumber,
			_layout.ManagingDb.TotalSize,
			_managingCodec.Decode,
			ct);
	}

	public async Task<Result<int>> ReadProtocolVersionAsync(CancellationToken ct = default)
	{
		if (!_transport.IsConnected)
		{
			return Result.Fail(new NotConnectedError());
		}

		try
		{
			var bytes = await ReadBytesAsync(
				_layout.ManagingDb.DbNumber,
				_layout.ManagingDb.VersionOffset,
				sizeof(int),
				ct);
			return bytes.Length < sizeof(int)
				? (Result<int>)Result.Fail(
					$"Protocol version read returned {bytes.Length} bytes, expected {sizeof(int)}")
				: Result.Ok(BinaryPrimitives.ReadInt32BigEndian(bytes));
		}
		catch (DriverNotConnectedException)
		{
			return DriverNotConnected();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "PLC protocol version read failed");
			return Result.Fail(new PlcCommandFailedError().CausedBy(ex));
		}
	}

	public async Task<Result<Recipe>> ReadRecipeFromPlcAsync(CancellationToken ct = default)
	{
		var managingAreaResult = await ReadManagingAreaAsync(ct);
		if (managingAreaResult.IsFailed)
		{
			return managingAreaResult.ToResult<Recipe>();
		}

		if (!managingAreaResult.Value.Committed)
		{
			return Result.Fail(new RecipeNotCommittedError());
		}

		var recipeDataResult = await ReadRecipeDataAsync(managingAreaResult.Value.RecipeLines, ct);
		return recipeDataResult.IsFailed ? recipeDataResult.ToResult<Recipe>() : _converter.ToRecipe(recipeDataResult.Value);
	}

	private async Task<Result<T>> ReadAndDecodeAsync<T>(
		int dbNumber, int size, Func<byte[], Result<T>> decode, CancellationToken ct)
	{
		if (!_transport.IsConnected)
		{
			return Result.Fail(new NotConnectedError());
		}

		try
		{
			var bytes = await ReadBytesAsync(dbNumber, 0, size, ct);
			return decode(bytes);
		}
		catch (DriverNotConnectedException)
		{
			return DriverNotConnected();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "PLC read/decode failed for DB {DbNumber}", dbNumber);
			return Result.Fail(new PlcCommandFailedError().CausedBy(ex));
		}
	}

	private async Task<Result<PlcRecipeData>> ReadRecipeDataAsync(int stepCount, CancellationToken ct)
	{
		if (!_transport.IsConnected)
		{
			return Result.Fail(new NotConnectedError());
		}

		try
		{
			var intHeaderBytes = await ReadBytesAsync(
				_layout.IntDb.DbNumber,
				0,
				_layout.IntDb.DataStartOffset,
				ct);
			var intCount = ArrayCodec.ReadArrayCurrentSize(intHeaderBytes, _layout.IntDb);

			var floatHeaderBytes = await ReadBytesAsync(
				_layout.FloatDb.DbNumber,
				0,
				_layout.FloatDb.DataStartOffset,
				ct);
			var floatCount = ArrayCodec.ReadArrayCurrentSize(floatHeaderBytes, _layout.FloatDb);

			var stringHeaderBytes = await ReadBytesAsync(
				_layout.StringDb.DbNumber,
				0,
				_layout.StringDb.DataStartOffset,
				ct);
			var stringCount = ArrayCodec.ReadArrayCurrentSize(stringHeaderBytes, _layout.StringDb);

			var intDataSize = _layout.IntDb.DataStartOffset + (intCount * ProtocolConstants.IntElementSize);
			var floatDataSize = _layout.FloatDb.DataStartOffset + (floatCount * ProtocolConstants.FloatElementSize);
			var stringDataSize = _layout.StringDb.DataStartOffset + (stringCount * _arrayCodec.WStringElementSize);

			var intData = await ReadBytesAsync(_layout.IntDb.DbNumber, 0, intDataSize, ct);
			var floatData = await ReadBytesAsync(_layout.FloatDb.DbNumber, 0, floatDataSize, ct);
			var stringData = await ReadBytesAsync(_layout.StringDb.DbNumber, 0, stringDataSize, ct);

			var intValues = _arrayCodec.DecodeIntArray(intData, intCount);
			var floatValues = _arrayCodec.DecodeFloatArray(floatData, floatCount);
			var stringValues = _arrayCodec.DecodeStringArray(stringData, stringCount);

			return Result.Ok(new PlcRecipeData(intValues, floatValues, stringValues, StepCount: stepCount));
		}
		catch (DriverNotConnectedException)
		{
			return DriverNotConnected();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogWarning(ex, "PLC recipe data read failed");
			return Result.Fail(new PlcCommandFailedError().CausedBy(ex));
		}
	}

	private async Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct)
	{
		var bytes = await _transport.ReadBytesAsync(dbNumber, startByte, count, ct);
		Interlocked.Exchange(ref _lastExchangeUtcTicks, DateTimeOffset.UtcNow.UtcTicks);

		return bytes;
	}

	private async Task<Result> WriteRecipeDataAsync(PlcRecipeData data, CancellationToken ct)
	{
		try
		{
			var writeManagingResult = await WriteManagingAreaAsync(
				new ManagingAreaPcData(Committed: false, RecipeLines: 0), ct);
			if (writeManagingResult.IsFailed)
			{
				return writeManagingResult;
			}

			var intBytes = _arrayCodec.EncodeIntArray(data.IntValues);
			var floatBytes = _arrayCodec.EncodeFloatArray(data.FloatValues);
			var stringBytes = _arrayCodec.EncodeStringArray(data.StringValues);

			await _transport.WriteBytesAsync(_layout.IntDb.DbNumber, 0, intBytes, ct);
			await _transport.WriteBytesAsync(_layout.FloatDb.DbNumber, 0, floatBytes, ct);
			await _transport.WriteBytesAsync(_layout.StringDb.DbNumber, 0, stringBytes, ct);

			var writeLinesResult = await WriteManagingAreaAsync(
				new ManagingAreaPcData(Committed: false, RecipeLines: data.StepCount), ct);
			return writeLinesResult.IsFailed
				? writeLinesResult
				: await WriteManagingAreaAsync(
				new ManagingAreaPcData(Committed: true, RecipeLines: data.StepCount), ct);
		}
		catch (DriverNotConnectedException)
		{
			return DriverNotConnected();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "PLC recipe data write failed");
			return Result.Fail(new PlcCommandFailedError().CausedBy(ex));
		}
	}

	private async Task<Result<bool>> VerifyWriteAsync(PlcRecipeData expected, CancellationToken ct)
	{
		var managingAreaResult = await ReadManagingAreaAsync(ct);
		if (managingAreaResult.IsFailed)
		{
			return managingAreaResult.ToResult<bool>();
		}

		if (managingAreaResult.Value.RecipeLines != expected.StepCount)
		{
			_logger.LogWarning(
				"PLC recipe_lines is {ActualLines}, written step count is {ExpectedLines}",
				managingAreaResult.Value.RecipeLines,
				expected.StepCount);
			return Result.Ok(false);
		}

		var actualResult = await ReadRecipeDataAsync(expected.StepCount, ct);
		return actualResult.IsFailed
			? actualResult.ToResult<bool>()
			: Result.Ok(PlcRecipeDataComparer.DataMatchesExpected(actualResult.Value, expected));
	}

	private async Task<Result> WriteManagingAreaAsync(ManagingAreaPcData data, CancellationToken ct)
	{
		try
		{
			var bytes = _managingCodec.EncodePcData(data);
			var managingDb = _layout.ManagingDb;

			// Write the two PC-owned fields at their own offsets only. The firmware-owned version
			// field is never on the wire, regardless of how the offsets are ordered in the layout.
			var committedSlice = bytes[managingDb.CommittedOffset..(managingDb.CommittedOffset + sizeof(byte))];
			await _transport.WriteBytesAsync(managingDb.DbNumber, managingDb.CommittedOffset, committedSlice, ct);

			var recipeLinesSlice = bytes[managingDb.RecipeLinesOffset..(managingDb.RecipeLinesOffset + sizeof(int))];
			await _transport.WriteBytesAsync(managingDb.DbNumber, managingDb.RecipeLinesOffset, recipeLinesSlice, ct);

			return Result.Ok();
		}
		catch (DriverNotConnectedException)
		{
			return DriverNotConnected();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "PLC managing-area write failed");
			return Result.Fail(new PlcCommandFailedError().CausedBy(ex));
		}
	}

	private Result DriverNotConnected()
	{
		_logger.LogDebug("PLC operation failed: the driver holds no PLC instance");
		return Result.Fail(new NotConnectedError());
	}
}
