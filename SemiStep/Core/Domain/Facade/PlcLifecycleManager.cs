using Core.Analysis;

using Domain.Plc;
using Domain.State;

using FluentResults;

using Serilog;

using TypesShared.Core;
using TypesShared.Domain;
using TypesShared.Plc;

namespace Domain.Facade;

public sealed class PlcLifecycleManager : IDisposable
{
	private readonly RecipeAnalyzer _analyzer;
	private readonly IS7Service _connectionService;
	private readonly RecipeHistoryManager _historyManager;
	private readonly RecipeStateManager _stateManager;
	private readonly IPlcSyncService _syncService;
	private readonly RecipeWorkspace _workspace;
	private Action<PlcConnectionState>? _connectionStateHandler;
	private bool _disposed;
	private bool _initialized;
	private bool _isSyncEnabled;
	private Recipe? _pendingPlcRecipe;

	internal PlcLifecycleManager(
		RecipeWorkspace workspace,
		IS7Service connectionService,
		RecipeAnalyzer analyzer,
		RecipeHistoryManager historyManager,
		RecipeStateManager stateManager,
		IPlcSyncService syncService)
	{
		_workspace = workspace;
		_connectionService = connectionService;
		_analyzer = analyzer;
		_historyManager = historyManager;
		_stateManager = stateManager;
		_syncService = syncService;
		_workspace.SetSyncEnabledProvider(() => _isSyncEnabled);
	}

	public bool IsSyncEnabled => _isSyncEnabled;
	public bool IsConnected => _connectionService.IsConnected;
	public bool IsRecipeActive => _connectionService.IsRecipeActive;
	public IObservable<PlcExecutionInfo> ExecutionState => _connectionService.ExecutionState;

	public PlcSyncStatus SyncStatus => _syncService.Status;
	public DateTimeOffset? LastSyncTime => _syncService.LastSyncTime;

	public IObservable<Result<PlcSessionSnapshot>> PlcState => _syncService.PlcState;

	public event Action<Recipe, Recipe>? PlcRecipeConflictDetected;

	public void Initialize()
	{
		if (_initialized)
		{
			return;
		}

		_initialized = true;
		_connectionStateHandler = OnConnectionStateChanged;
		_connectionService.StateChanged += _connectionStateHandler;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_connectionStateHandler is not null)
		{
			_connectionService.StateChanged -= _connectionStateHandler;
		}
	}

	public async Task<Result> EnableSync(PlcConfiguration config)
	{
		if (_isSyncEnabled)
		{
			return Result.Ok();
		}

		try
		{
			_isSyncEnabled = true;
			_syncService.SetSyncEnabled(true);
			await _connectionService.ConnectAsync(config.Connection);
		}
		catch (Exception ex)
		{
			_isSyncEnabled = false;
			_syncService.SetSyncEnabled(false);
			Log.Warning("PLC connection failed: {Message}", ex.Message);
			return Result.Fail(ex.Message);
		}

		return Result.Ok();
	}

	public async Task DisableSync()
	{
		_isSyncEnabled = false;
		_syncService.SetSyncEnabled(false);
		_syncService.Reset();

		try
		{
			await _connectionService.DisconnectAsync();
		}
		catch (Exception ex)
		{
			Log.Warning("Error while disconnecting from PLC: {Message}", ex.Message);
		}
	}

	public async Task<Result> LoadRecipeFromPlcAsync()
	{
		var loadResult = await _connectionService.ReadRecipeFromPlcAsync();
		if (loadResult.IsFailed)
		{
			return loadResult.ToResult();
		}

		return _workspace.LoadAsCurrent(loadResult.Value);
	}

	public Result ResolveConflict(bool keepLocal)
	{
		if (keepLocal)
		{
			_pendingPlcRecipe = null;
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);

			return Result.Ok();
		}

		if (_pendingPlcRecipe is null)
		{
			Log.Warning("ResolveConflict called with keepLocal=false but no pending PLC recipe exists.");

			return Result.Fail("No pending PLC recipe to resolve.");
		}

		LoadPlcRecipeIntoState(_pendingPlcRecipe);
		_pendingPlcRecipe = null;

		return Result.Ok();
	}

	private void OnConnectionStateChanged(PlcConnectionState state)
	{
		_syncService.UpdateConnectionState(state);

		if (state == PlcConnectionState.Disconnected && _isSyncEnabled)
		{
			_syncService.Reset();
		}
		else if (state == PlcConnectionState.Connected && _isSyncEnabled)
		{
			_ = PerformReconnectReconciliationAsync().ContinueWith(
				t => Log.Error(t.Exception, "Unhandled error in reconnect reconciliation"),
				TaskContinuationOptions.OnlyOnFaulted);
		}
	}

	private async Task PerformReconnectReconciliationAsync()
	{
		var managingAreaResult = await _connectionService.ReadManagingAreaAsync();
		if (managingAreaResult.IsFailed)
		{
			Log.Warning(
				"Could not read managing area during reconnect reconciliation: {Errors}",
				string.Join("; ", managingAreaResult.Errors.Select(e => e.Message)));
			NotifyLocalRecipe();
			return;
		}

		if (!managingAreaResult.Value.Committed)
		{
			NotifyLocalRecipe();
			return;
		}

		var plcRecipeResult = await _connectionService.ReadRecipeFromPlcAsync();
		if (plcRecipeResult.IsFailed)
		{
			Log.Warning(
				"Could not read PLC recipe during reconciliation: {Errors}",
				string.Join("; ", plcRecipeResult.Errors.Select(e => e.Message)));
			NotifyLocalRecipe();
			return;
		}

		var plcRecipe = plcRecipeResult.Value;
		var localRecipe = _stateManager.Current;

		if (localRecipe.Steps.Count == 0 && plcRecipe.Steps.Count > 0)
		{
			LoadPlcRecipeIntoState(plcRecipe);
			return;
		}

		if (plcRecipe.Steps.Count > 0 && !localRecipe.Equals(plcRecipe))
		{
			_pendingPlcRecipe = plcRecipe;
			PlcRecipeConflictDetected?.Invoke(localRecipe, plcRecipe);
			return;
		}

		NotifyLocalRecipe();
	}

	private void NotifyLocalRecipe()
	{
		_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
	}

	private void LoadPlcRecipeIntoState(Recipe recipe)
	{
		_historyManager.Clear();
		var snapshot = _analyzer.Analyze(recipe);
		_stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			Log.Warning(
				"PLC recipe analysis produced errors: {Errors}",
				string.Join("; ", snapshot.Errors.Select(e => e.Message)));
		}

		if (_isSyncEnabled)
		{
			_syncService.NotifyRecipeChanged(_stateManager.Current, _stateManager.IsValid);
		}
	}
}
