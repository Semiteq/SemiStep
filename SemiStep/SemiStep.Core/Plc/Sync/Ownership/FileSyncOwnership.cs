using System.Text.Json;

using FluentResults;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Plc.Configuration;

namespace SemiStep.Core.Plc.Sync.Ownership;

public sealed class FileSyncOwnership : IPlcSyncOwnership
{
	private const string UnavailableMessage = "PLC sync is owned by another instance or the lock is unavailable.";
	private const string OwnedByUnknownMessage = "PLC sync is owned by another instance.";

	private readonly string _lockRoot;
	private readonly ILogger<FileSyncOwnership> _logger;
	private readonly SyncLockRootProvisioner _rootProvisioner = new();

	public FileSyncOwnership(ILogger<FileSyncOwnership> logger)
		: this(DefaultLockRoot(), logger)
	{
	}

	public FileSyncOwnership(string lockRoot, ILogger<FileSyncOwnership>? logger = null)
	{
		_lockRoot = lockRoot;
		_logger = logger ?? NullLogger<FileSyncOwnership>.Instance;
	}

	public Result<ISyncOwnershipLease> TryAcquire(PlcConnectionSettings endpoint)
	{
		var lockFilePath = BuildLockFilePath(endpoint);

		try
		{
			_rootProvisioner.EnsureRoot(_lockRoot);
		}
		catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
		{
			return RefuseUnavailable(exception);
		}

		try
		{
			return Acquire(lockFilePath);
		}
		catch (UnauthorizedAccessException exception)
		{
			return RefuseUnavailable(exception);
		}
		catch (IOException)
		{
			return RefuseHeldBy(lockFilePath);
		}
	}

	private static Result<ISyncOwnershipLease> Acquire(string lockFilePath)
	{
		var owner = OwnerInfo.ForCurrentProcess();
		var lockFile = new FileStream(lockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

		try
		{
			WriteOwner(lockFile, owner);
		}
		catch
		{
			lockFile.Dispose();
			throw;
		}

		return Result.Ok<ISyncOwnershipLease>(new FileSyncOwnershipLease(lockFile, owner));
	}

	private static void WriteOwner(FileStream lockFile, OwnerInfo owner)
	{
		var payload = JsonSerializer.SerializeToUtf8Bytes(owner);
		lockFile.SetLength(0);
		lockFile.Write(payload);
		lockFile.Flush(flushToDisk: true);
	}

	private static Result<ISyncOwnershipLease> RefuseHeldBy(string lockFilePath)
	{
		var holder = TryReadOwner(lockFilePath);

		if (holder is null)
		{
			return Result.Fail<ISyncOwnershipLease>(OwnedByUnknownMessage);
		}

		return Result.Fail<ISyncOwnershipLease>(new OwnedByAnotherInstanceError(holder));
	}

	private static OwnerInfo? TryReadOwner(string lockFilePath)
	{
		try
		{
			using var reader = new FileStream(
				lockFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

			using var memory = new MemoryStream();
			reader.CopyTo(memory);

			if (memory.Length == 0)
			{
				return null;
			}

			return JsonSerializer.Deserialize<OwnerInfo>(memory.ToArray());
		}
		catch (Exception exception) when (exception is IOException
			or UnauthorizedAccessException
			or JsonException)
		{
			return null;
		}
	}

	private Result<ISyncOwnershipLease> RefuseUnavailable(Exception detail)
	{
		_logger.LogWarning(detail, "PLC sync lock is unavailable; refusing ownership.");
		return Result.Fail<ISyncOwnershipLease>(UnavailableMessage);
	}

	private string BuildLockFilePath(PlcConnectionSettings endpoint)
	{
		var token = SyncOwnershipEndpointToken.For(endpoint);
		return Path.Combine(_lockRoot, $"plc-sync-{token}.lock");
	}

	private static string DefaultLockRoot()
	{
		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
			"SemiStep",
			"locks");
	}
}
