using System.IO;

using FluentAssertions;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "Ownership")]
[Trait("Category", "Integration")]
public sealed class FileSyncOwnershipTests : IDisposable
{
	private readonly TempDirectory _lockRoot = new();
	private readonly PlcConnectionSettings _endpoint = new("192.168.0.150", 102, 0, 2);

	public void Dispose()
	{
		_lockRoot.Dispose();
	}

	[Fact]
	public void TryAcquire_FreeEndpoint_Succeeds()
	{
		var ownership = CreateOwnership();

		var result = ownership.TryAcquire(_endpoint);

		result.IsSuccess.Should().BeTrue();
		result.Value.Owner.ProcessId.Should().Be(Environment.ProcessId);
		result.Value.Dispose();
	}

	[Fact]
	public void TryAcquire_WhileHeld_IsRefused()
	{
		var ownership = CreateOwnership();
		using var firstLease = ownership.TryAcquire(_endpoint).Value;

		var secondResult = ownership.TryAcquire(_endpoint);

		secondResult.IsFailed.Should().BeTrue();
	}

	[Fact]
	public void TryAcquire_AfterReleasingLease_Succeeds()
	{
		var ownership = CreateOwnership();
		var firstResult = ownership.TryAcquire(_endpoint);
		firstResult.IsSuccess.Should().BeTrue();

		firstResult.Value.Dispose();

		var secondResult = ownership.TryAcquire(_endpoint);
		secondResult.IsSuccess.Should().BeTrue();
		secondResult.Value.Dispose();
	}

	[Fact]
	public void TryAcquire_DifferentEndpointWhileHeld_Succeeds()
	{
		var ownership = CreateOwnership();
		using var firstLease = ownership.TryAcquire(_endpoint).Value;

		var otherEndpoint = new PlcConnectionSettings("192.168.0.151", 102, 0, 2);
		var secondResult = ownership.TryAcquire(otherEndpoint);

		secondResult.IsSuccess.Should().BeTrue();
		secondResult.Value.Dispose();
	}

	[Fact]
	public void TryAcquire_WhileHeld_ExposesHolderOwnerInfo()
	{
		var ownership = CreateOwnership();
		using var firstLease = ownership.TryAcquire(_endpoint).Value;

		var secondResult = ownership.TryAcquire(_endpoint);

		var holderError = secondResult.Errors
			.OfType<OwnedByAnotherInstanceError>()
			.Single();
		holderError.Holder.ProcessId.Should().Be(firstLease.Owner.ProcessId);
		holderError.Holder.MachineName.Should().Be(firstLease.Owner.MachineName);
		holderError.Holder.UserName.Should().Be(firstLease.Owner.UserName);
	}

	[Fact]
	public void LeaseDispose_CalledTwice_IsSafe()
	{
		var ownership = CreateOwnership();
		var lease = ownership.TryAcquire(_endpoint).Value;

		lease.Dispose();
		var secondDispose = () => lease.Dispose();

		secondDispose.Should().NotThrow();
	}

	[Fact]
	public void TryAcquire_WhileHeldWithCorruptMetadata_RefusesWithoutHolderInfo()
	{
		var locksRoot = Path.Combine(_lockRoot.Path, "locks");
		Directory.CreateDirectory(locksRoot);
		var lockFilePath = Path.Combine(locksRoot, $"plc-sync-{SyncOwnershipEndpointToken.For(_endpoint)}.lock");

		using var holder = new FileStream(lockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
		holder.Write("not-json-at-all"u8);
		holder.Flush(flushToDisk: true);
		var ownership = new FileSyncOwnership(locksRoot);

		var result = ownership.TryAcquire(_endpoint);

		result.IsFailed.Should().BeTrue("the lock is held, so acquisition must be refused");
		result.Errors.OfType<OwnedByAnotherInstanceError>().Should().BeEmpty(
			"unreadable holder metadata must fall back to a generic refusal, not a typed owner error");
	}

	[Fact]
	public void TryAcquire_WhileHeldWithEmptyMetadata_RefusesWithoutHolderInfo()
	{
		var locksRoot = Path.Combine(_lockRoot.Path, "locks");
		Directory.CreateDirectory(locksRoot);
		var lockFilePath = Path.Combine(locksRoot, $"plc-sync-{SyncOwnershipEndpointToken.For(_endpoint)}.lock");

		using var holder = new FileStream(lockFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
		var ownership = new FileSyncOwnership(locksRoot);

		var result = ownership.TryAcquire(_endpoint);

		result.IsFailed.Should().BeTrue("the lock is held, so acquisition must be refused");
		result.Errors.OfType<OwnedByAnotherInstanceError>().Should().BeEmpty(
			"empty holder metadata must fall back to a generic refusal, not a typed owner error");
	}

	[Fact]
	public void TryAcquire_WhenLockFileCannotBeOpenedForWrite_ReturnsRefusal()
	{
		var locksRoot = Path.Combine(_lockRoot.Path, "locks");
		Directory.CreateDirectory(locksRoot);
		var lockFilePath = Path.Combine(locksRoot, $"plc-sync-{SyncOwnershipEndpointToken.For(_endpoint)}.lock");
		File.WriteAllText(lockFilePath, "preexisting");
		File.SetAttributes(lockFilePath, FileAttributes.ReadOnly);
		var ownership = new FileSyncOwnership(locksRoot);

		try
		{
			var result = ownership.TryAcquire(_endpoint);

			result.IsFailed.Should().BeTrue(
				"an UnauthorizedAccessException opening the lock file must map to a clean refusal, not a throw");
			result.Errors.OfType<OwnedByAnotherInstanceError>().Should().BeEmpty(
				"the ACL-blocked acquire path returns a generic refusal without holder metadata");
		}
		finally
		{
			File.SetAttributes(lockFilePath, FileAttributes.Normal);
		}
	}

	[Fact]
	public void TryAcquire_WhenLockRootProvisioningFails_ReturnsRefusal()
	{
		var inaccessibleRoot = Path.Combine(_lockRoot.Path, "not-a-directory");
		File.WriteAllText(inaccessibleRoot, "occupied");
		var ownership = new FileSyncOwnership(inaccessibleRoot);

		var result = ownership.TryAcquire(_endpoint);

		result.IsFailed.Should().BeTrue();
	}

	private FileSyncOwnership CreateOwnership()
	{
		return new FileSyncOwnership(Path.Combine(_lockRoot.Path, "locks"));
	}
}
