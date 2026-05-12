using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

namespace SemiStep.Core.Plc;

public sealed class PlcLifecycleManager : IDisposable
{
	private readonly IS7Connection _connection;
	private readonly IS7ExecutionStream _executionStream;
	private readonly ImportedRecipeValidator _importedRecipeValidator;
	private readonly ILogger<PlcLifecycleManager> _logger;
	private readonly IS7Reader _reader;
	private readonly IPlcSyncService _syncService;
	private readonly RecipeWorkspace _workspace;
	private Action<PlcConnectionState>? _connectionStateHandler;
	private bool _disposed;
	private bool _initialized;
	private Recipe? _pendingPlcRecipe;

	public PlcLifecycleManager(
		RecipeWorkspace workspace,
		IS7Connection connection,
		IS7Reader reader,
		IS7ExecutionStream executionStream,
		IPlcSyncService syncService,
		ImportedRecipeValidator importedRecipeValidator,
		ILogger<PlcLifecycleManager> logger)
	{
		_workspace = workspace;
		_connection = connection;
		_reader = reader;
		_executionStream = executionStream;
		_syncService = syncService;
		_importedRecipeValidator = importedRecipeValidator;
		_logger = logger;
	}

	public bool IsSyncEnabled => _syncService.IsSyncEnabled;
	public bool IsConnected => _connection.IsConnected;
	public bool IsRecipeActive => _executionStream.IsRecipeActive;
	public IObservable<PlcExecutionInfo> ExecutionState => _executionStream.ExecutionState;

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
		_connection.StateChanged += _connectionStateHandler;
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
			_connection.StateChanged -= _connectionStateHandler;
		}
	}

	public async Task<Result> EnableSync(PlcConfiguration config)
	{
		if (_syncService.IsSyncEnabled)
		{
			return Result.Ok();
		}

		try
		{
			_syncService.SetSyncEnabled(true);
			await _connection.ConnectAsync(config.Connection);
		}
		catch (Exception ex)
		{
			_syncService.SetSyncEnabled(false);
			_logger.LogWarning("PLC connection failed: {Message}", ex.Message);
			return Result.Fail(ex.Message);
		}

		return Result.Ok();
	}

	public async Task DisableSync()
	{
		_syncService.SetSyncEnabled(false);
		_syncService.Reset();

		try
		{
			await _connection.DisconnectAsync();
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Error while disconnecting from PLC: {Message}", ex.Message);
		}
	}

	public async Task<Result> LoadRecipeFromPlcAsync()
	{
		var loadResult = await _reader.ReadRecipeFromPlcAsync();
		if (loadResult.IsFailed)
		{
			return loadResult.ToResult();
		}

		return ValidateAndLoad(loadResult.Value);
	}

	private Result ValidateAndLoad(Recipe recipe)
	{
		return _workspace.LoadAsCurrentValidated(recipe, _importedRecipeValidator);
	}

	public Result ResolveConflict(bool keepLocal)
	{
		if (keepLocal)
		{
			_pendingPlcRecipe = null;
			_syncService.NotifyRecipeChanged(_workspace.CurrentRecipe, _workspace.IsValid);

			return Result.Ok();
		}

		if (_pendingPlcRecipe is null)
		{
			_logger.LogWarning("ResolveConflict called with keepLocal=false but no pending PLC recipe exists.");

			return Result.Fail("No pending PLC recipe to resolve.");
		}

		var result = _workspace.LoadAsCurrent(_pendingPlcRecipe);
		_pendingPlcRecipe = null;

		return result;
	}

	private void OnConnectionStateChanged(PlcConnectionState state)
	{
		_syncService.UpdateConnectionState(state);

		if (state == PlcConnectionState.Disconnected && _syncService.IsSyncEnabled)
		{
			_syncService.Reset();
		}
		else if (state == PlcConnectionState.Connected && _syncService.IsSyncEnabled)
		{
			_ = PerformReconnectReconciliationAsync().ContinueWith(
				t => _logger.LogError(t.Exception, "Unhandled error in reconnect reconciliation"),
				TaskContinuationOptions.OnlyOnFaulted);
		}
	}

	private async Task PerformReconnectReconciliationAsync()
	{
		var managingAreaResult = await _reader.ReadManagingAreaAsync();
		if (managingAreaResult.IsFailed)
		{
			_logger.LogWarning(
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

		var plcRecipeResult = await _reader.ReadRecipeFromPlcAsync();
		if (plcRecipeResult.IsFailed)
		{
			_logger.LogWarning(
				"Could not read PLC recipe during reconciliation: {Errors}",
				string.Join("; ", plcRecipeResult.Errors.Select(e => e.Message)));
			NotifyLocalRecipe();
			return;
		}

		var plcRecipe = plcRecipeResult.Value;
		var localRecipe = _workspace.CurrentRecipe;

		if (localRecipe.Steps.Count == 0 && plcRecipe.Steps.Count > 0)
		{
			var loadResult = ValidateAndLoad(plcRecipe);
			if (loadResult.IsFailed)
			{
				_logger.LogWarning(
					"PLC recipe analysis errors during reconnect: {Errors}",
					string.Join("; ", loadResult.Errors.Select(e => e.Message)));
			}
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
		_syncService.NotifyRecipeChanged(_workspace.CurrentRecipe, _workspace.IsValid);
	}
}
