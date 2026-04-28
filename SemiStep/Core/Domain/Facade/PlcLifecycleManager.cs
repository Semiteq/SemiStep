using Domain.State;

using FluentResults;

using Serilog;

using TypesShared.Core;
using TypesShared.Domain;
using TypesShared.Plc;

namespace Domain.Facade;

/// <summary>
/// Manages PLC connection lifecycle and sync-related reconciliation on behalf of <see cref="DomainFacade"/>.
/// </summary>
internal sealed class PlcLifecycleManager(
	IS7Service connectionService,
	ICoreService coreService,
	RecipeHistoryManager historyManager,
	RecipeStateManager stateManager,
	IPlcSyncService syncService,
	Action<Recipe, Recipe> raiseConflictDetected)
	: IDisposable
{
	private Action<PlcConnectionState>? _connectionStateHandler;
	private bool _disposed;
	private bool _isSyncEnabled;
	private Recipe? _pendingPlcRecipe;

	public bool IsSyncEnabled => _isSyncEnabled;

	public void Initialize()
	{
		_connectionStateHandler = OnConnectionStateChanged;
		connectionService.StateChanged += _connectionStateHandler;
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
			connectionService.StateChanged -= _connectionStateHandler;
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
			syncService.SetSyncEnabled(true);
			await connectionService.ConnectAsync(config.Connection);
		}
		catch (Exception ex)
		{
			_isSyncEnabled = false;
			syncService.SetSyncEnabled(false);
			Log.Warning("PLC connection failed: {Message}", ex.Message);
			return Result.Fail(ex.Message);
		}

		return Result.Ok();
	}

	public async Task DisableSync()
	{
		_isSyncEnabled = false;
		syncService.SetSyncEnabled(false);
		syncService.Reset();

		try
		{
			await connectionService.DisconnectAsync();
		}
		catch (Exception ex)
		{
			Log.Warning("Error while disconnecting from PLC: {Message}", ex.Message);
		}
	}

	public Result ResolveConflict(bool keepLocal, RecipeStateManager stateManager)
	{
		if (keepLocal)
		{
			_pendingPlcRecipe = null;
			syncService.NotifyRecipeChanged(stateManager.Current, stateManager.IsValid);

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
		syncService.UpdateConnectionState(state);

		if (state == PlcConnectionState.Disconnected && _isSyncEnabled)
		{
			syncService.Reset();
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
		var managingAreaResult = await connectionService.ReadManagingAreaAsync();
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

		var plcRecipeResult = await connectionService.ReadRecipeFromPlcAsync();
		if (plcRecipeResult.IsFailed)
		{
			Log.Warning(
				"Could not read PLC recipe during reconciliation: {Errors}",
				string.Join("; ", plcRecipeResult.Errors.Select(e => e.Message)));
			NotifyLocalRecipe();
			return;
		}

		var plcRecipe = plcRecipeResult.Value;
		var localRecipe = stateManager.Current;

		if (localRecipe.Steps.Count == 0 && plcRecipe.Steps.Count > 0)
		{
			LoadPlcRecipeIntoState(plcRecipe);
			return;
		}

		if (plcRecipe.Steps.Count > 0 && !localRecipe.Equals(plcRecipe))
		{
			_pendingPlcRecipe = plcRecipe;
			raiseConflictDetected(localRecipe, plcRecipe);
			return;
		}

		NotifyLocalRecipe();
	}

	private void NotifyLocalRecipe()
	{
		syncService.NotifyRecipeChanged(stateManager.Current, stateManager.IsValid);
	}

	private void LoadPlcRecipeIntoState(Recipe recipe)
	{
		historyManager.Clear();
		var snapshot = coreService.AnalyzeRecipe(recipe);
		stateManager.Update(snapshot);

		if (snapshot.IsFailed)
		{
			Log.Warning(
				"PLC recipe analysis produced errors: {Errors}",
				string.Join("; ", snapshot.Errors.Select(e => e.Message)));
		}

		if (_isSyncEnabled)
		{
			syncService.NotifyRecipeChanged(stateManager.Current, stateManager.IsValid);
		}
	}
}
