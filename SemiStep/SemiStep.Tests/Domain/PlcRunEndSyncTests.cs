using System.Collections.Concurrent;
using System.Reactive.Disposables;

using AwesomeAssertions;

using FluentResults;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

namespace SemiStep.Tests.Domain;

/// <summary>"Keep local" chosen while the PLC runs another recipe: the write waits for the run to end.</summary>
[Trait("Component", "Domain")]
[Trait("Area", "Reconnect")]
[Trait("Category", "Integration")]
public sealed class PlcRunEndSyncTests : IAsyncLifetime
{
	private const int WaitActionId = 10;

	private static readonly TimeSpan _syncTimeout = TimeSpan.FromSeconds(5);
	private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(20);
	private static readonly TimeSpan _quietPeriod = TimeSpan.FromMilliseconds(1500);

	private readonly StubS7Service _s7Service = new();
	private readonly StoringS7Transport _transport = new();
	private readonly ConcurrentQueue<IError> _faults = new();
	private readonly ConcurrentQueue<PlcSyncStatus> _statuses = new();
	private ServiceProvider _services = null!;
	private PlcLifecycleManager _plc = null!;
	private RecipeSession _session = null!;
	private PlcTransactionExecutor _executor = null!;
	private AppConfiguration _configuration = null!;
	private IDisposable? _subscriptions;

	public async ValueTask InitializeAsync()
	{
		var loaded = await ConfigFacade.LoadAndValidateAsync(TestConfigLocator.GetConfigDirectory());
		_configuration = loaded.Value;

		_services = new ServiceCollection()
			.AddLogging()
			.AddSingleton(_configuration)
			.AddRecipe()
			.AddCsv()
			.AddClipboard()
			.AddSingleton<IS7Connection>(_s7Service)
			.AddSingleton<IS7Reader>(_s7Service)
			.AddSingleton<IS7ExecutionStream>(_s7Service)
			.AddSingleton<IPlcSyncService>(sp => BuildCoordinator(sp.GetRequiredService<RecipeMetadataRegistry>()))
			.AddSingleton<IPlcSyncOwnership, StubPlcSyncOwnership>()
			.BuildServiceProvider();

		_session = _services.GetRequiredService<RecipeSession>();
		_plc = _services.GetRequiredService<PlcLifecycleManager>();
		_plc.Initialize();
		_session.Reset();

		var faults = _plc.Faults.Subscribe(_faults.Enqueue);
		var statuses = _plc.PlcState.Subscribe(snapshot => _statuses.Enqueue(snapshot.SyncStatus));
		_subscriptions = new CompositeDisposable(faults, statuses);
	}

	public async ValueTask DisposeAsync()
	{
		_subscriptions?.Dispose();
		_plc.Dispose();
		await _services.DisposeAsync();
	}

	[Fact]
	public async Task KeepLocalDuringRun_WritesTheLocalRecipe_WhenTheRunEnds()
	{
		await KeepLocalDuringRunAsync();

		await SetRunActiveAsync(false);
		await TestHelpers.WaitUntilAsync(
			() => _plc.SyncStatus == PlcSyncStatus.Synced,
			_syncTimeout,
			_pollInterval,
			TestContext.Current.CancellationToken);

		var written = await _executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);
		written.IsSuccess.Should().BeTrue();
		written.Value.Should().Be(_session.Current, "the recipe the operator kept reaches the PLC after the run");
	}

	[Fact]
	public async Task KeepLocalDuringRun_AttemptsNoWrite_WhileTheRunLasts()
	{
		await KeepLocalDuringRunAsync();

		await SetRunActiveAsync(true);
		await SetRunActiveAsync(true);
		await Task.Delay(TimeSpan.FromSeconds(2.5), TestContext.Current.CancellationToken);

		_faults.OfType<RecipeActiveError>().Should().ContainSingle("the parked recipe is not retried during the run");
		_statuses.Should().NotContain(PlcSyncStatus.Syncing, "no write starts while the PLC runs a recipe");
	}

	[Fact]
	public async Task EditWhileParked_WritesTheNewestRecipeOnce_WhenTheRunEnds()
	{
		await KeepLocalDuringRunAsync();
		_session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();
		await TestHelpers.WaitUntilAsync(
			() => _faults.OfType<RecipeActiveError>().Count() == 2,
			_syncTimeout,
			_pollInterval,
			TestContext.Current.CancellationToken);

		await SetRunActiveAsync(false);
		await TestHelpers.WaitUntilAsync(
			() => _plc.SyncStatus == PlcSyncStatus.Synced,
			_syncTimeout,
			_pollInterval,
			TestContext.Current.CancellationToken);
		await Task.Delay(_quietPeriod, TestContext.Current.CancellationToken);

		var written = await _executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);
		written.IsSuccess.Should().BeTrue();
		written.Value.Should().Be(_session.Current, "the edit made during the run reaches the PLC");
		_statuses.Count(status => status == PlcSyncStatus.Syncing).Should().Be(1, "the run end writes once");
	}

	[Fact]
	public async Task LinkLossWhileParked_WritesNothingUntilReconnect_ThenReconciles()
	{
		await KeepLocalDuringRunAsync();
		_s7Service.RaiseStateChanged(PlcConnectionState.Disconnected);

		await SetRunActiveAsync(false);
		await Task.Delay(_quietPeriod, TestContext.Current.CancellationToken);
		_statuses.Should().NotContain(PlcSyncStatus.Syncing, "a link loss clears the park");

		var conflictRaised = false;
		_plc.PlcRecipeConflictDetected += (_, _) => conflictRaised = true;
		_s7Service.RaiseStateChanged(PlcConnectionState.Connected);
		await TestHelpers.WaitUntilAsync(() => conflictRaised, TestContext.Current.CancellationToken);
		_statuses.Should().NotContain(PlcSyncStatus.Syncing, "the reconnect reconciliation decides the next write");
	}

	private PlcSyncCoordinator BuildCoordinator(RecipeMetadataRegistry registry)
	{
		var plcConfiguration = _configuration.PlcConfiguration;
		var layout = plcConfiguration.Layout;
		var arrayCodec = new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, registry.GetStringMaxLength());
		_executor = new PlcTransactionExecutor(
			_transport,
			new RecipeConverter(registry),
			arrayCodec,
			plcConfiguration,
			NullLogger<PlcTransactionExecutor>.Instance);

		return new PlcSyncCoordinator(_executor, _s7Service, _s7Service, NullLoggerFactory.Instance);
	}

	/// <summary>Connects during a run, raises the conflict, keeps the local recipe and waits for the deferral.</summary>
	private async Task KeepLocalDuringRunAsync()
	{
		_session.AppendStep(WaitActionId).IsSuccess.Should().BeTrue();
		var plcRecipe = RecipeStubs.WithSteps(WaitActionId, 1);
		_s7Service.ManagingAreaToReturn = new PlcManagingAreaState(Committed: true, RecipeLines: plcRecipe.StepCount);
		_s7Service.RecipeToReturn = plcRecipe;
		await SetRunActiveAsync(true);

		(await _plc.EnableSync(_configuration.PlcConfiguration)).IsSuccess.Should().BeTrue();
		var conflictRaised = false;
		_plc.PlcRecipeConflictDetected += (_, _) => conflictRaised = true;
		_s7Service.RaiseStateChanged(PlcConnectionState.Connected);
		await TestHelpers.WaitUntilAsync(() => conflictRaised, TestContext.Current.CancellationToken);

		_plc.ResolveConflict(keepLocal: true).IsSuccess.Should().BeTrue();
		await TestHelpers.WaitUntilAsync(
			() => _faults.OfType<RecipeActiveError>().Any(),
			_syncTimeout,
			_pollInterval,
			TestContext.Current.CancellationToken);
	}

	private async Task SetRunActiveAsync(bool isActive)
	{
		var executionDb = _configuration.PlcConfiguration.Layout.ExecutionDb;
		await _transport.WriteBytesAsync(
			executionDb.DbNumber,
			executionDb.RecipeActiveOffset,
			[isActive ? (byte)1 : (byte)0],
			TestContext.Current.CancellationToken);
		_s7Service.PushExecutionState(PlcExecutionInfo.Empty with { RecipeActive = isActive });
	}
}
