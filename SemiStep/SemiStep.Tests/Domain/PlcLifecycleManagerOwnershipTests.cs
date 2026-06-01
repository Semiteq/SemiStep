using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Domain;

[Trait("Component", "Domain")]
[Trait("Area", "Ownership")]
[Trait("Category", "Integration")]
public sealed class PlcLifecycleManagerOwnershipTests
{
	private static async Task<(PlcLifecycleManager Plc, StubS7Service S7Service, StubPlcSyncService SyncService, StubPlcSyncOwnership Ownership)> BuildAsync()
	{
		var configDir = TestConfigLocator.GetConfigDirectory("Standard");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var s7Service = new StubS7Service();
		var syncService = new StubPlcSyncService();
		var ownership = new StubPlcSyncOwnership();

		var services = new ServiceCollection()
			.AddLogging()
			.AddSingleton(configLoadResult.Value)
			.AddRecipe()
			.AddCsv()
			.AddClipboard()
			.AddSingleton<IS7Connection>(s7Service)
			.AddSingleton<IS7Reader>(s7Service)
			.AddSingleton<IS7ExecutionStream>(s7Service)
			.AddSingleton<IPlcSyncService>(syncService)
			.AddSingleton<IPlcSyncOwnership>(ownership)
			.BuildServiceProvider();

		var session = services.GetRequiredService<RecipeSession>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		session.Reset();

		return (plc, s7Service, syncService, ownership);
	}

	[Fact]
	public async Task EnableSync_WhenOwnershipAcquired_AcquiresThenConnects()
	{
		var (plc, s7Service, syncService, ownership) = await BuildAsync();

		var result = await plc.EnableSync(PlcConfiguration.Default);

		result.IsSuccess.Should().BeTrue("acquiring ownership must let the connect proceed");
		ownership.TryAcquireCallCount.Should().Be(1,
			"EnableSync must attempt to acquire ownership exactly once");
		s7Service.ConnectCallCount.Should().Be(1,
			"a successful acquire must be followed by a connect");
		syncService.IsSyncEnabled.Should().BeTrue();
	}

	[Fact]
	public async Task EnableSync_WhenAlreadyEnabled_DoesNotReacquireOwnership()
	{
		var (plc, _, _, ownership) = await BuildAsync();
		(await plc.EnableSync(PlcConfiguration.Default)).IsSuccess.Should().BeTrue();

		var secondResult = await plc.EnableSync(PlcConfiguration.Default);

		secondResult.IsSuccess.Should().BeTrue("a redundant EnableSync must short-circuit to success");
		ownership.TryAcquireCallCount.Should().Be(1,
			"the already-enabled short-circuit must not attempt to acquire ownership again");
	}

	[Fact]
	public async Task EnableSync_AfterDisableSync_ReacquiresOwnershipAndConnectsAgain()
	{
		var (plc, s7Service, _, ownership) = await BuildAsync();
		(await plc.EnableSync(PlcConfiguration.Default)).IsSuccess.Should().BeTrue();
		var firstLease = ownership.LastLease;
		await plc.DisableSync();

		var reEnableResult = await plc.EnableSync(PlcConfiguration.Default);

		reEnableResult.IsSuccess.Should().BeTrue("re-enabling after a clean release must succeed");
		ownership.TryAcquireCallCount.Should().Be(2,
			"each enable cycle must acquire ownership afresh");
		s7Service.ConnectCallCount.Should().Be(2,
			"re-enabling must connect to the PLC again");
		ownership.LastLease.Should().NotBeSameAs(firstLease,
			"re-enabling must hand out a fresh lease, not reuse the released one");
	}

	[Fact]
	public async Task EnableSync_WhenOwnershipRefused_FailsWithOwnerMessageAndDoesNotConnect()
	{
		var (plc, s7Service, syncService, ownership) = await BuildAsync();
		ownership.ShouldRefuse = true;
		ownership.RefusalOwner = new OwnerInfo(
			ProcessId: 4321,
			MachineName: "OTHER-MACHINE",
			UserName: "rival-user",
			AcquiredUtc: DateTimeOffset.UnixEpoch);

		var result = await plc.EnableSync(PlcConfiguration.Default);

		result.IsFailed.Should().BeTrue("a refused acquire must fail EnableSync");
		result.HasError<OwnedByAnotherInstanceError>().Should().BeTrue(
			"the failure must carry the typed owner error so the UI can surface holder metadata");
		result.Errors[0].Message.Should().Contain("rival-user",
			"the refusal message must name the current owner");
		s7Service.ConnectCallCount.Should().Be(0,
			"a refused acquire must not connect to the PLC");
		syncService.IsSyncEnabled.Should().BeFalse(
			"a refused acquire must leave sync disabled");
		plc.IsSyncEnabled.Should().BeFalse();
	}

	[Fact]
	public async Task DisableSync_ReleasesLeaseExactlyOnce()
	{
		var (plc, _, _, ownership) = await BuildAsync();
		(await plc.EnableSync(PlcConfiguration.Default)).IsSuccess.Should().BeTrue();
		var lease = ownership.LastLease;
		lease.Should().NotBeNull();

		await plc.DisableSync();

		lease!.DisposeCallCount.Should().Be(1,
			"disabling sync must release the ownership lease exactly once");
	}

	[Fact]
	public async Task DisableSync_CalledTwice_DoesNotReleaseLeaseAgain()
	{
		var (plc, _, _, ownership) = await BuildAsync();
		(await plc.EnableSync(PlcConfiguration.Default)).IsSuccess.Should().BeTrue();
		var lease = ownership.LastLease;

		await plc.DisableSync();
		await plc.DisableSync();

		lease!.DisposeCallCount.Should().Be(1,
			"a second DisableSync must not dispose the lease again (idempotent release)");
	}

	[Fact]
	public async Task FailedProtocolHandshake_ReleasesLeaseExactlyOnce()
	{
		var (plc, s7Service, syncService, ownership) = await BuildAsync();
		s7Service.ProtocolVersionToReturn = FluentResults.Result.Ok(int.MaxValue);

		var result = await plc.EnableSync(PlcConfiguration.Default);

		result.IsFailed.Should().BeTrue("a protocol mismatch must fail EnableSync");
		syncService.IsSyncEnabled.Should().BeFalse();
		ownership.TryAcquireCallCount.Should().Be(1,
			"a handshake-failing enable must still have acquired ownership exactly once before connecting");
		ownership.LastLease.Should().NotBeNull();
		ownership.LastLease!.DisposeCallCount.Should().Be(1,
			"a failed handshake rollback must release the ownership lease exactly once");
	}

	[Fact]
	public async Task EnableSync_WhenProtocolVersionReadThrowsCanceled_ReleasesLeaseAndDisablesSync()
	{
		var (plc, s7Service, syncService, ownership) = await BuildAsync();
		s7Service.ProtocolVersionReadShouldThrowCanceled = true;

		var result = await plc.EnableSync(PlcConfiguration.Default);

		result.IsFailed.Should().BeTrue(
			"a handshake read that throws after a successful connect must fail EnableSync, not propagate");
		ownership.TryAcquireCallCount.Should().Be(1,
			"the lease must have been acquired before the throwing handshake");
		ownership.LastLease.Should().NotBeNull();
		ownership.LastLease!.DisposeCallCount.Should().Be(1,
			"a handshake that throws must release the ownership lease, not orphan it");
		syncService.IsSyncEnabled.Should().BeFalse(
			"a handshake that throws must reset sync state");
		plc.IsSyncEnabled.Should().BeFalse();
	}

	[Fact]
	public async Task Dispose_ReleasesLeaseExactlyOnce()
	{
		var (plc, _, _, ownership) = await BuildAsync();
		(await plc.EnableSync(PlcConfiguration.Default)).IsSuccess.Should().BeTrue();
		var lease = ownership.LastLease;

		plc.Dispose();

		lease!.DisposeCallCount.Should().Be(1,
			"disposing the manager must release a held lease exactly once");
	}

	[Fact]
	public async Task Dispose_AfterDisableSync_DoesNotReleaseLeaseAgain()
	{
		var (plc, _, _, ownership) = await BuildAsync();
		(await plc.EnableSync(PlcConfiguration.Default)).IsSuccess.Should().BeTrue();
		var lease = ownership.LastLease;

		await plc.DisableSync();
		plc.Dispose();

		lease!.DisposeCallCount.Should().Be(1,
			"the lease released on DisableSync must not be released again on Dispose");
	}
}
