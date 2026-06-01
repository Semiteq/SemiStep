namespace SemiStep.Core.Plc.Sync.Ownership;

public sealed class FileSyncOwnershipLease : ISyncOwnershipLease
{
	private readonly FileStream _lockFile;
	private bool _disposed;

	public FileSyncOwnershipLease(FileStream lockFile, OwnerInfo owner)
	{
		_lockFile = lockFile;
		Owner = owner;
	}

	public OwnerInfo Owner { get; }

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
