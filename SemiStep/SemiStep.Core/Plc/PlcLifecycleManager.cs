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
	private readonly CancellationTokenSource _lifetimeCts = new();
	private readonly ILogger<PlcLifecycleManager> _logger;
	private readonly object _pendingLock = new();
	private readonly IS7Reader _reader;
	private readonly RecipeSession _session;
	private readonly IPlcSyncService _syncService;
	private Action<PlcConnectionState>? _connectionStateHandler;
	private bool _disposed;
	private bool _initialized;
	private Recipe? _pendingPlcRecipe;
	private Func<Recipe, Task<Result>>? _reconnectApplyCallback;

	public PlcLifecycleManager(
		RecipeSession session,
		IS7Connection connection,
		IS7Reader reader,
		IS7ExecutionStream executionStream,
		IPlcSyncService syncService,
		ImportedRecipeValidator importedRecipeValidator,
		ILogger<PlcLifecycleManager> logger)
	{
		_session = session;
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

		try
		{
			_lifetimeCts.Cancel();
		}
		catch (ObjectDisposedException)
		{
		}

		_lifetimeCts.Dispose();
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

	public async Task<Result<Recipe>> ReadRecipeFromPlcAsync()
	{
		return await _reader.ReadRecipeFromPlcAsync();
	}

	public Result ApplyRecipeFromPlc(Recipe recipe)
	{
		return ValidateAndLoad(recipe);
	}

	/// <summary>
	/// Registers the callback invoked by the reconnect-reconciliation path when an empty
	/// local recipe is replaced with the recipe read from the PLC. The callback owns
	/// the UI-thread marshalling and mutation-signal dispatch so the grid and dependent
	/// view-models refresh; the lifecycle manager only knows when an apply should occur.
	/// </summary>
	public void RegisterReconnectApplyCallback(Func<Recipe, Task<Result>> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);

		if (_reconnectApplyCallback is not null)
		{
			throw new InvalidOperationException(
				"Reconnect apply callback has already been registered.");
		}

		_reconnectApplyCallback = callback;
	}

	private Result ValidateAndLoad(Recipe recipe)
	{
		return _session.LoadAsCurrentValidated(recipe, _importedRecipeValidator);
	}

	public Result ResolveConflict(bool keepLocal)
	{
		if (keepLocal)
		{
			lock (_pendingLock)
			{
				_pendingPlcRecipe = null;
			}

			_syncService.NotifyRecipeChanged(_session.Current, _session.IsValid);

			return Result.Ok();
		}

		Recipe? pending;
		lock (_pendingLock)
		{
			pending = _pendingPlcRecipe;
			_pendingPlcRecipe = null;
		}

		if (pending is null)
		{
			_logger.LogWarning("ResolveConflict called with keepLocal=false but no pending PLC recipe exists.");

			return Result.Fail("No pending PLC recipe to resolve.");
		}

		return _session.LoadAsCurrent(pending);
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
			if (_disposed)
			{
				return;
			}

			var token = _lifetimeCts.Token;
			_ = PerformReconnectReconciliationAsync(token).ContinueWith(
				t => _logger.LogError(t.Exception, "Unhandled error in reconnect reconciliation"),
				TaskContinuationOptions.OnlyOnFaulted);
		}
	}

	private async Task PerformReconnectReconciliationAsync(CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}

		var managingAreaResult = await _reader.ReadManagingAreaAsync();

		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}

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

		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}

		if (plcRecipeResult.IsFailed)
		{
			_logger.LogWarning(
				"Could not read PLC recipe during reconciliation: {Errors}",
				string.Join("; ", plcRecipeResult.Errors.Select(e => e.Message)));
			NotifyLocalRecipe();
			return;
		}

		var plcRecipe = plcRecipeResult.Value;
		var localRecipe = _session.Current;

		if (localRecipe.Steps.Count == 0 && plcRecipe.Steps.Count > 0)
		{
			await ApplyReconnectPlcRecipeAsync(plcRecipe, cancellationToken);
			return;
		}

		if (plcRecipe.Steps.Count > 0 && !localRecipe.Equals(plcRecipe))
		{
			lock (_pendingLock)
			{
				_pendingPlcRecipe = plcRecipe;
			}

			PlcRecipeConflictDetected?.Invoke(localRecipe, plcRecipe);
			return;
		}

		NotifyLocalRecipe();
	}

	private async Task ApplyReconnectPlcRecipeAsync(Recipe plcRecipe, CancellationToken cancellationToken)
	{
		var callback = _reconnectApplyCallback;
		if (callback is null)
		{
			_logger.LogWarning(
				"Reconnect reconciliation discarded PLC recipe: no apply callback registered.");
			return;
		}

		Result applyResult;
		try
		{
			applyResult = await callback(plcRecipe);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Reconnect apply callback threw");
			return;
		}

		if (cancellationToken.IsCancellationRequested)
		{
			return;
		}

		if (applyResult.IsFailed)
		{
			_logger.LogWarning(
				"PLC recipe analysis errors during reconnect: {Errors}",
				string.Join("; ", applyResult.Errors.Select(e => e.Message)));
		}
	}

	private void NotifyLocalRecipe()
	{
		_syncService.NotifyRecipeChanged(_session.Current, _session.IsValid);
	}
}
