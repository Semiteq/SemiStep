using Domain.Ports;

using S7.Net;
using S7.Protocol;
using S7.Serialization;

using Shared.Entities;

namespace S7.Facade;

public sealed class S7Facade : IPlcConnection
{
	private Plc? _plc;
	private PlcConnectionSettings? _settings;

	public bool IsConnected => _plc?.IsConnected ?? false;

	public async Task ConnectAsync(PlcConnectionSettings settings, CancellationToken ct = default)
	{
		_settings = settings;

		_plc = new Plc(
			CpuType.S71500,
			settings.IpAddress,
			(short)settings.Rack,
			(short)settings.Slot);

		await _plc.OpenAsync(ct);
	}

	public Task DisconnectAsync(CancellationToken ct = default)
	{
		_plc?.Close();
		_plc = null;
		return Task.CompletedTask;
	}

	public async Task<PlcRecipeData> ReadRecipeDataAsync(CancellationToken ct = default)
	{
		CheckIfConnectedOrThrow();

		var intHeaderBytes = await ReadBytesInternalAsync(
			ProtocolConstants.IntDataDbNumber,
			0,
			DataArrayLayout.DataStartOffset,
			ct);
		var intCount = ArrayCodec.ReadArrayCurrentSize(intHeaderBytes);

		var floatHeaderBytes = await ReadBytesInternalAsync(
			ProtocolConstants.FloatDataDbNumber,
			0,
			DataArrayLayout.DataStartOffset,
			ct);
		var floatCount = ArrayCodec.ReadArrayCurrentSize(floatHeaderBytes);

		var stringHeaderBytes = await ReadBytesInternalAsync(
			ProtocolConstants.StringDataDbNumber,
			0,
			DataArrayLayout.DataStartOffset,
			ct);
		var stringCount = ArrayCodec.ReadArrayCurrentSize(stringHeaderBytes);

		var intDataSize = DataArrayLayout.DataStartOffset + intCount * DataArrayLayout.IntElementSize;
		var floatDataSize = DataArrayLayout.DataStartOffset + floatCount * DataArrayLayout.FloatElementSize;
		var stringDataSize = DataArrayLayout.DataStartOffset + stringCount * DataArrayLayout.WStringElementSize;

		var intData = await ReadBytesInternalAsync(ProtocolConstants.IntDataDbNumber, 0, intDataSize, ct);
		var floatData = await ReadBytesInternalAsync(ProtocolConstants.FloatDataDbNumber, 0, floatDataSize, ct);
		var stringData = await ReadBytesInternalAsync(ProtocolConstants.StringDataDbNumber, 0, stringDataSize, ct);

		var intValues = ArrayCodec.DecodeIntArray(intData, intCount);
		var floatValues = ArrayCodec.DecodeFloatArray(floatData, floatCount);
		var stringValues = ArrayCodec.DecodeStringArray(stringData, stringCount);

		var stepCount = CalculateStepCountFromIntArray(intCount);

		return new PlcRecipeData(intValues, floatValues, stringValues, stepCount);
	}

	public async Task WriteRecipeDataAsync(PlcRecipeData data, CancellationToken ct = default)
	{
		CheckIfConnectedOrThrow();

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
			? ChecksumCalculator.ComputeStringArrayCrc32(data.StringValues, DataArrayLayout.WStringMaxChars)
			: 0u;

		var pcData = new ManagingAreaPcData(
			Status: PcStatus.Writing,
			TransactionId: transactionId,
			ChecksumInt: checksumInt,
			ChecksumFloat: checksumFloat,
			ChecksumString: checksumString,
			RecipeLines: (uint)data.StepCount);

		await WriteManagingAreaAsync(pcData, ct);

		var intBytes = ArrayCodec.EncodeIntArray(data.IntValues);
		var floatBytes = ArrayCodec.EncodeFloatArray(data.FloatValues);
		var stringBytes = ArrayCodec.EncodeStringArray(data.StringValues);

		await WriteBytesInternalAsync(ProtocolConstants.IntDataDbNumber, 0, intBytes, ct);
		await WriteBytesInternalAsync(ProtocolConstants.FloatDataDbNumber, 0, floatBytes, ct);
		await WriteBytesInternalAsync(ProtocolConstants.StringDataDbNumber, 0, stringBytes, ct);

		var commitData = pcData with { Status = PcStatus.CommitRequest };
		await WriteManagingAreaAsync(commitData, ct);

		await WaitForPlcConfirmationAsync(transactionId, ct);

		var idleData = pcData with { Status = PcStatus.Idle };
		await WriteManagingAreaAsync(idleData, ct);
	}

	public async Task<PlcExecutionState> ReadExecutionStateAsync(CancellationToken ct = default)
	{
		CheckIfConnectedOrThrow();

		var bytes = await ReadBytesInternalAsync(
			ProtocolConstants.ExecutionDbNumber,
			0,
			ExecutionAreaLayout.TotalSize,
			ct);

		return ExecutionStateCodec.Decode(bytes);
	}

	public async Task<ManagingAreaState> ReadManagingAreaAsync(CancellationToken ct = default)
	{
		CheckIfConnectedOrThrow();

		var bytes = await ReadBytesInternalAsync(
			ProtocolConstants.ManagingDbNumber,
			0,
			ManagingAreaLayout.TotalSize,
			ct);

		return ManagingAreaCodec.Decode(bytes);
	}

	public async Task WriteManagingAreaAsync(ManagingAreaPcData data, CancellationToken ct = default)
	{
		CheckIfConnectedOrThrow();

		var bytes = ManagingAreaCodec.EncodePcData(data);
		await WriteBytesInternalAsync(ProtocolConstants.ManagingDbNumber, 0, bytes, ct);
	}

	public async Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		CheckIfConnectedOrThrow();
		return await ReadBytesInternalAsync(dbNumber, startByte, count, ct);
	}

	public async Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		CheckIfConnectedOrThrow();
		await WriteBytesInternalAsync(dbNumber, startByte, data, ct);
	}

	public async ValueTask DisposeAsync()
	{
		if (_plc is not null)
		{
			await DisconnectAsync();
		}
	}

	private async Task<byte[]> ReadBytesInternalAsync(int dbNumber, int startByte, int count, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		return await _plc!.ReadBytesAsync(DataType.DataBlock, dbNumber, startByte, count);
	}

	private async Task WriteBytesInternalAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		await _plc!.WriteBytesAsync(DataType.DataBlock, dbNumber, startByte, data);
	}

	private async Task WaitForPlcConfirmationAsync(uint transactionId, CancellationToken ct)
	{
		var deadline = DateTime.UtcNow.AddSeconds(ProtocolConstants.CommitTimeoutSeconds);

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

				case PlcSyncStatus.CrcComputing:
				case PlcSyncStatus.Busy:
					await Task.Delay(ProtocolConstants.PollIntervalMs, ct);
					break;

				default:
					await Task.Delay(ProtocolConstants.PollIntervalMs, ct);
					break;
			}
		}

		throw new PlcSyncTimeoutException("Timeout waiting for PLC confirmation");
	}

	private void CheckIfConnectedOrThrow()
	{
		if (!IsConnected)
		{
			throw new PlcNotConnectedException("Not connected to PLC");
		}
	}

	private static int CalculateStepCountFromIntArray(int intCount)
	{
		return intCount > 0 ? intCount : 0;
	}
}
