using Shared.Entities;

namespace Domain.Ports;

public interface IPlcConnection : IAsyncDisposable
{
	bool IsConnected { get; }

	Task ConnectAsync(PlcConnectionSettings settings, CancellationToken ct = default);

	Task DisconnectAsync(CancellationToken ct = default);

	Task<PlcRecipeData> ReadRecipeDataAsync(CancellationToken ct = default);

	Task WriteRecipeDataAsync(PlcRecipeData data, CancellationToken ct = default);

	Task<PlcExecutionState> ReadExecutionStateAsync(CancellationToken ct = default);

	Task<ManagingAreaState> ReadManagingAreaAsync(CancellationToken ct = default);

	Task WriteManagingAreaAsync(ManagingAreaPcData data, CancellationToken ct = default);

	Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default);

	Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default);
}

public sealed record PlcExecutionState(
	bool RecipeActive,
	int ActualLine,
	float StepCurrentTime,
	int ForLoopCount1,
	int ForLoopCount2,
	int ForLoopCount3);

public sealed record ManagingAreaState(
	PcStatus PcStatus,
	uint PcTransactionId,
	uint PcChecksumInt,
	uint PcChecksumFloat,
	uint PcChecksumString,
	uint PcRecipeLines,
	PlcSyncStatus PlcStatus,
	PlcSyncError PlcError,
	uint PlcStoredId,
	uint PlcChecksumInt,
	uint PlcChecksumFloat,
	uint PlcChecksumString);

public sealed record ManagingAreaPcData(
	PcStatus Status,
	uint TransactionId,
	uint ChecksumInt,
	uint ChecksumFloat,
	uint ChecksumString,
	uint RecipeLines);

public enum PcStatus : ushort
{
	Idle = 0,
	Writing = 1,
	CommitRequest = 2
}

public enum PlcSyncStatus : ushort
{
	Idle = 0,
	Busy = 1,
	CrcComputing = 2,
	Success = 3,
	Error = 4
}

public enum PlcSyncError : ushort
{
	NoError = 0,
	ChecksumMismatchInt = 1,
	ChecksumMismatchFloat = 2,
	ChecksumMismatchString = 3,
	ChecksumMismatchMultiple = 4,
	Timeout = 5
}
