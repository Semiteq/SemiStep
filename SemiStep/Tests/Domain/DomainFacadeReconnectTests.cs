using System.Collections.Immutable;

using ClipBoard;

using Config;
using Config.Facade;

using Core;

using Csv;

using Domain;
using Domain.Facade;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Tests.Helpers;

using TypesShared.Config;
using TypesShared.Core;
using TypesShared.Domain;
using TypesShared.Plc;

using Xunit;

namespace Tests.Domain;

[Trait("Component", "Domain")]
[Trait("Area", "Reconnect")]
[Trait("Category", "Unit")]
public sealed class DomainFacadeReconnectTests
{
	private const int WaitActionId = 10;

	private static async Task<(DomainFacade Facade, StubIs7Service S7Service, StubPlcSyncService SyncService)>
		BuildFacadeAsync()
	{
		var configDir = TestConfigLocator.GetConfigDirectory("Standard");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var s7Service = new StubIs7Service();
		var syncService = new StubPlcSyncService();

		var services = new ServiceCollection()
			.AddSingleton(configLoadResult.Value)
			.AddRecipe()
			.AddDomain()
			.AddCsv()
			.AddClipboard()
			.AddSingleton<IS7Service>(s7Service)
			.AddSingleton<IPlcSyncService>(syncService)
			.BuildServiceProvider();

		var facade = services.GetRequiredService<DomainFacade>();
		facade.Initialize();

		return (facade, s7Service, syncService);
	}

	private static Recipe BuildSingleStepRecipe()
	{
		var step = new Step(
			WaitActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty);

		return new Recipe(ImmutableList.Create(step));
	}

	[Fact]
	public async Task StateChanged_Connected_WhenRecipesDiffer_FiresConflictDetected()
	{
		var (facade, s7Service, _) = await BuildFacadeAsync();

		// Populate local recipe so it is non-empty.
		var appendResult = facade.AppendStep(WaitActionId);
		appendResult.IsSuccess.Should().BeTrue();

		// Configure stub: committed=true, PLC recipe different from local.
		var plcRecipe = BuildSingleStepRecipe();
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = plcRecipe;

		// Activate sync so the relay handles Connected events.
		var enableResult = await facade.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		Recipe? conflictLocalRecipe = null;
		Recipe? conflictPlcRecipe = null;
		facade.PlcRecipeConflictDetected += (local, plc) =>
		{
			conflictLocalRecipe = local;
			conflictPlcRecipe = plc;
		};

		// Simulate an auto-reconnect: StateChanged fires Connected while sync is active.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		// Allow the fire-and-forget reconciliation task to complete.
		await Task.Delay(200);

		conflictLocalRecipe.Should().NotBeNull(
			"PlcRecipeConflictDetected must fire when local and PLC recipes differ and both are non-empty");
		conflictPlcRecipe.Should().Be(plcRecipe);
	}

	[Fact]
	public async Task StateChanged_Connected_WhenNotCommitted_PushesLocalRecipe()
	{
		var (facade, s7Service, syncService) = await BuildFacadeAsync();

		// Configure stub: committed=false, so reconciliation should push local recipe.
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);

		var enableResult = await facade.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// Capture the call count after EnableSync to measure only the reconnect-triggered push.
		var countBeforeStateChange = syncService.NotifyRecipeChangedCallCount;

		// Simulate an auto-reconnect.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);
		await Task.Delay(200);

		syncService.NotifyRecipeChangedCallCount.Should().BeGreaterThan(countBeforeStateChange,
			"when committed=false the facade must push the local recipe to the PLC via NotifyRecipeChanged");
	}

	[Fact]
	public async Task StateChanged_Disconnected_WhenSyncEnabled_CallsReset()
	{
		var (facade, s7Service, syncService) = await BuildFacadeAsync();

		var enableResult = await facade.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// Simulate a connection drop while sync is active.
		s7Service.RaiseStateChanged(PlcConnectionState.Disconnected);

		syncService.WasResetCalled.Should().BeTrue(
			"IPlcSyncService.Reset() must be called when the PLC disconnects while sync is enabled");
	}

	[Fact]
	public async Task DisableSync_CallsResetOnSyncService()
	{
		var (facade, _, syncService) = await BuildFacadeAsync();

		var enableResult = await facade.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		await facade.DisableSync();

		syncService.WasResetCalled.Should().BeTrue(
			"IPlcSyncService.Reset() must be called when sync is manually disabled");
	}
}
