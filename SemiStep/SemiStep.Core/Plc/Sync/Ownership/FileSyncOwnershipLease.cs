namespace SemiStep.Core.Plc.Sync.Ownership;

public sealed class FileSyncOwnershipLease(FileStream lockFile, OwnerInfo owner) : ISyncOwnershipLease
{
	private readonly FileStream _lockFile = lockFile;
	private bool _disposed;

	public OwnerInfo Owner { get; } = owner;

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_lockFile.Dispose();
	}
}
