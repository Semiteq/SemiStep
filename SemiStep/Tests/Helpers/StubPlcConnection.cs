using Domain.Ports;

using Shared.Entities;

namespace Tests.Helpers;

public sealed class StubPlcConnection : IPlcConnection, IDisposable
{
	private bool _isConnected;
	private PlcRecipeData _storedData = new([], [], [], 0);

	public bool IsConnected => _isConnected;

	public Task ConnectAsync(PlcConnectionSettings settings, CancellationToken ct = default)
	{
		_isConnected = true;
		return Task.CompletedTask;
	}

	public Task DisconnectAsync(CancellationToken ct = default)
	{
		_isConnected = false;
		return Task.CompletedTask;
	}

	public Task<PlcRecipeData> ReadRecipeDataAsync(CancellationToken ct = default)
	{
		return Task.FromResult(_storedData);
	}

	public Task WriteRecipeDataAsync(PlcRecipeData data, CancellationToken ct = default)
	{
		_storedData = data;
		return Task.CompletedTask;
	}

	public Task<PlcExecutionState> ReadExecutionStateAsync(CancellationToken ct = default)
	{
		return Task.FromResult(new PlcExecutionState(
			RecipeActive: false,
			ActualLine: 0,
			StepCurrentTime: 0f,
			ForLoopCount1: 0,
			ForLoopCount2: 0,
			ForLoopCount3: 0));
	}

	public Task<ManagingAreaState> ReadManagingAreaAsync(CancellationToken ct = default)
	{
		return Task.FromResult(new ManagingAreaState(
			PcStatus: PcStatus.Idle,
			PcTransactionId: 0,
			PcChecksumInt: 0,
			PcChecksumFloat: 0,
			PcChecksumString: 0,
			PcRecipeLines: 0,
			PlcStatus: PlcSyncStatus.Idle,
			PlcError: PlcSyncError.NoError,
			PlcStoredId: 0,
			PlcChecksumInt: 0,
			PlcChecksumFloat: 0,
			PlcChecksumString: 0));
	}

	public Task WriteManagingAreaAsync(ManagingAreaPcData data, CancellationToken ct = default)
	{
		return Task.CompletedTask;
	}

	public Task<byte[]> ReadBytesAsync(int dbNumber, int startByte, int count, CancellationToken ct = default)
	{
		return Task.FromResult(new byte[count]);
	}

	public Task WriteBytesAsync(int dbNumber, int startByte, byte[] data, CancellationToken ct = default)
	{
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		_isConnected = false;
		return ValueTask.CompletedTask;
	}

	public void Dispose()
	{
		_isConnected = false;
	}
}
