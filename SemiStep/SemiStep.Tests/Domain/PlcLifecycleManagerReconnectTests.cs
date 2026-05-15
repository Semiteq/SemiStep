using System.Collections.Immutable;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Domain;

[Trait("Component", "Domain")]
[Trait("Area", "Reconnect")]
[Trait("Category", "Integration")]
public sealed class PlcLifecycleManagerReconnectTests
{
	private const int WaitActionId = 10;

	private static async Task<(PlcLifecycleManager Plc, RecipeWorkspace Workspace, RecipeEditor Editor, StubS7Service S7Service, StubPlcSyncService SyncService)>
		BuildAsync()
	{
		var configDir = TestConfigLocator.GetConfigDirectory("Standard");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var s7Service = new StubS7Service();
		var syncService = new StubPlcSyncService();

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
			.BuildServiceProvider();

		var workspace = services.GetRequiredService<RecipeWorkspace>();
		var editor = services.GetRequiredService<RecipeEditor>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		workspace.Reset();

		return (plc, workspace, editor, s7Service, syncService);
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
		var (plc, _, editor, s7Service, _) = await BuildAsync();

		// Populate local recipe so it is non-empty.
		var appendResult = editor.AppendStep(WaitActionId);
		appendResult.IsSuccess.Should().BeTrue();

		// Configure stub: committed=true, PLC recipe different from local.
		var plcRecipe = BuildSingleStepRecipe();
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: 1);
		s7Service.RecipeToReturn = plcRecipe;

		// Activate sync so the relay handles Connected events.
		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		Recipe? conflictLocalRecipe = null;
		Recipe? conflictPlcRecipe = null;
		plc.PlcRecipeConflictDetected += (local, plcRec) =>
		{
			conflictLocalRecipe = local;
			conflictPlcRecipe = plcRec;
		};

		// Simulate an auto-reconnect: StateChanged fires Connected while sync is active.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		// (a) Observable state: wait until the fire-and-forget reconciliation has raised the conflict event.
		await TestHelpers.WaitUntilAsync(
			() => conflictLocalRecipe is not null,
			cancellationToken: TestContext.Current.CancellationToken);

		conflictLocalRecipe.Should().NotBeNull(
			"PlcRecipeConflictDetected must fire when local and PLC recipes differ and both are non-empty");
		conflictPlcRecipe.Should().Be(plcRecipe);
	}

	[Fact]
	public async Task StateChanged_Connected_WhenNotCommitted_PushesLocalRecipe()
	{
		var (plc, _, _, s7Service, syncService) = await BuildAsync();

		// Configure stub: committed=false, so reconciliation should push local recipe.
		s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: false, RecipeLines: 0);

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// Capture the call count after EnableSync to measure only the reconnect-triggered push.
		var countBeforeStateChange = syncService.NotifyRecipeChangedCallCount;

		// Simulate an auto-reconnect.
		s7Service.RaiseStateChanged(PlcConnectionState.Connected);

		// (a) Observable state: wait until the reconcile-on-reconnect pushes the local recipe.
		await TestHelpers.WaitUntilAsync(
			() => syncService.NotifyRecipeChangedCallCount > countBeforeStateChange,
			cancellationToken: TestContext.Current.CancellationToken);

		syncService.NotifyRecipeChangedCallCount.Should().BeGreaterThan(countBeforeStateChange,
			"when committed=false the manager must push the local recipe to the PLC via NotifyRecipeChanged");
	}

	[Fact]
	public async Task StateChanged_Disconnected_WhenSyncEnabled_CallsReset()
	{
		var (plc, _, _, s7Service, syncService) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		// Simulate a connection drop while sync is active.
		s7Service.RaiseStateChanged(PlcConnectionState.Disconnected);

		syncService.WasResetCalled.Should().BeTrue(
			"IPlcSyncService.Reset() must be called when the PLC disconnects while sync is enabled");
	}

	[Fact]
	public async Task DisableSync_CallsResetOnSyncService()
	{
		var (plc, _, _, _, syncService) = await BuildAsync();

		var enableResult = await plc.EnableSync(PlcConfiguration.Default);
		enableResult.IsSuccess.Should().BeTrue();

		await plc.DisableSync();

		syncService.WasResetCalled.Should().BeTrue(
			"IPlcSyncService.Reset() must be called when sync is manually disabled");
	}
}
