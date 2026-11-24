using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

namespace SemiStep.Core.Plc;

public sealed class PlcLifecycleManager(
	RecipeSession session,
	IS7Connection connection,
	IS7Reader reader,
	IS7ExecutionStream executionStream,
	IPlcSyncService syncService,
	IPlcSyncOwnership syncOwnership,
	ImportedRecipeValidator importedRecipeValidator,
	ILogger<PlcLifecycleManager> logger) : IDisposable
{
	internal const string SyncEnableCancelledMessage = "PLC sync enable cancelled";

	internal const string ReconciliationStartedMessage =
		"PLC reconciliation started: the connection reported Connected while sync is enabled.";

	internal const string ReconciliationOutcomePrefix = "PLC reconciliation outcome ";

	/// <summary>
	/// Used alone where no PLC-side fact was established.
	/// </summary>
	private const string ShortOutcomeTemplate =
		ReconciliationOutcomePrefix + "{Outcome}: local {LocalStepCount} steps (valid {LocalValid})";

	private const string LongOutcomeTemplate =
		ShortOutcomeTemplate
		+ ", PLC {PlcStepCount} steps, committed {Committed}, conflict subscriber {HasConflictSubscriber}";

	private static readonly TimeSpan _disableWriteWaitTimeout = TimeSpan.FromSeconds(5);

	private readonly IS7Connection _connection = connection;
	private readonly IS7ExecutionStream _executionStream = executionStream;
	private readonly ImportedRecipeValidator _importedRecipeValidator = importedRecipeValidator;
	private readonly CancellationTokenSource _lifetimeCts = new();
	private readonly ILogger<PlcLifecycleManager> _logger = logger;
	private readonly Lock _pendingLock = new();
	private readonly IS7Reader _reader = reader;
	private readonly RecipeSession _session = session;
	private readonly IPlcSyncOwnership _syncOwnership = syncOwnership;
	private readonly IPlcSyncService _syncService = syncService;
	private Action<PlcConnectionState>? _connectionStateHandler;
	private bool _disposed;
	private bool _initialized;
	private ISyncOwnershipLease? _ownershipLease;
	private Recipe? _pendingPlcRecipe;
	private Func<Recipe, Task<Result>>? _reconnectApplyCallback;

	public bool IsSyncEnabled => _syncService.IsSyncEnabled;
	public bool IsConnected => _connection.IsConnected;
	public bool IsRecipeActive => _executionStream.IsRecipeActive;
	public IObservable<PlcExecutionInfo> ExecutionState => _executionStream.ExecutionState;

	public PlcSyncStatus SyncStatus => _syncService.Status;
	public DateTimeOffset? LastSyncTime => _syncService.LastSyncTime;

	public IObservable<PlcSessionSnapshot> PlcState => _syncService.PlcState;

	public IObservable<IError> Faults => _syncService.Faults;

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
		ReleaseOwnershipLease();
	}

	public async Task<Result> EnableSync(PlcConfiguration config)
	{
		if (_syncService.IsSyncEnabled)
		{
			return Result.Ok();
		}

		var ownershipResult = _syncOwnership.TryAcquire(config.Connection);
		if (ownershipResult.IsFailed)
		{
			_logger.LogWarning(
				"PLC sync ownership refused: {Errors}",
				string.Join("; ", ownershipResult.Errors.Select(e => e.Message)));
			return ownershipResult.ToResult();
		}

		_ownershipLease = ownershipResult.Value;

		var lifeToken = _lifetimeCts.Token;

		try
		{
			_syncService.SetSyncEnabled(true);
			await _connection.ConnectAsync(config.Connection);

			var versionResult = await ValidateProtocolVersionAsync(lifeToken);
			if (versionResult.IsFailed)
			{
				await FailProtocolVersionHandshakeAsync();
				return versionResult;
			}

			return Result.Ok();
		}
		catch (OperationCanceledException) when (lifeToken.IsCancellationRequested)
		{
			RollbackSyncEnable();
			return Result.Fail(SyncEnableCancelledMessage);
		}
		catch (Exception ex)
		{
			RollbackSyncEnable();
			_logger.LogWarning("Enabling PLC sync failed: {Message}", ex.Message);
			return Result.Fail(ex.Message);
		}
	}

	private async Task<Result> ValidateProtocolVersionAsync(CancellationToken ct)
	{
		var versionResult = await _reader.ReadProtocolVersionAsync(ct);
		return versionResult.IsFailed
			? versionResult.ToResult()
			: versionResult.Value != ProtocolConstants.ProtocolVersion
			? Result.Fail(
				new ProtocolVersionMismatchError(ProtocolConstants.ProtocolVersion, versionResult.Value))
			: Result.Ok();
	}

	private async Task FailProtocolVersionHandshakeAsync()
	{
		RollbackSyncEnable();
		await _connection.DisconnectAsync();
	}

	private void RollbackSyncEnable()
	{
		_syncService.SetSyncEnabled(false);
		ReleaseOwnershipLease();
	}

	/// <summary>
	/// The lease is released last: until the write in flight and the session end, another instance must not write.
	/// </summary>
	public async Task DisableSync()
	{
		_syncService.SetSyncEnabled(false);
		_syncService.ResetForDisable();
		await WaitForWriteInFlightAsync();

		try
		{
			await _connection.DisconnectAsync();
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Error while disconnecting from PLC: {Message}", ex.Message);
		}
		finally
		{
			ReleaseOwnershipLease();
		}
	}

	private async Task WaitForWriteInFlightAsync()
	{
		using var timeout = new CancellationTokenSource(_disableWriteWaitTimeout);
		try
		{
			await _syncService.WaitForPendingSyncAsync(timeout.Token);
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			_logger.LogWarning(
				"Sync attempt did not finish within {Timeout}; disconnecting from PLC anyway",
				_disableWriteWaitTimeout);
		}
	}

	private void ReleaseOwnershipLease()
	{
		var lease = _ownershipLease;
		if (lease is null)
		{
			return;
		}

		_ownershipLease = null;
		lease.Dispose();
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
			_syncService.HandleConnectionLost();
		}
		else if (state == PlcConnectionState.Connected && _syncService.IsSyncEnabled)
		{
			if (_disposed)
			{
				_logger.LogWarning(
					"PLC reconciliation dropped: the lifecycle manager was disposed while the connection "
					+ "state change was in flight.");
				return;
			}

			_logger.LogInformation(ReconciliationStartedMessage);

			var token = _lifetimeCts.Token;
			_ = PerformReconnectReconciliationAsync(token).ContinueWith(
				t => _logger.LogError(t.Exception, "Unhandled error in reconnect reconciliation"),
				TaskContinuationOptions.OnlyOnFaulted);
		}
	}

	private async Task PerformReconnectReconciliationAsync(CancellationToken cancellationToken)
	{
		try
		{
			LogOutcome(await RunReconciliationAsync(cancellationToken));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			LogOutcome(BuildResultFromSession(PlcReconciliationOutcome.Cancelled));
		}
		catch (OperationCanceledException ex)
		{
			// Unfiltered: a filter mirroring the one above would leave the exception uncaught when
			// Dispose cancels the lifetime token between the two filter evaluations, and the run
			// would end with a begin line and no outcome line.
			LogOutcome(BuildResultFromSession(PlcReconciliationOutcome.TimedOut), ex);
		}
	}

	private async Task<PlcReconciliationResult> RunReconciliationAsync(CancellationToken cancellationToken)
	{
		var versionResult = await ValidateProtocolVersionAsync(cancellationToken);
		if (versionResult.IsFailed)
		{
			_logger.LogWarning(
				"Aborting reconnect reconciliation: protocol version validation failed: {Errors}",
				string.Join("; ", versionResult.Errors.Select(e => e.Message)));
			await FailProtocolVersionHandshakeAsync();

			return BuildResultFromSession(versionResult.HasError<ProtocolVersionMismatchError>()
				? PlcReconciliationOutcome.ProtocolVersionMismatch
				: PlcReconciliationOutcome.ProtocolVersionCheckFailed);
		}

		var managingAreaResult = await _reader.ReadManagingAreaAsync(cancellationToken);

		if (managingAreaResult.IsFailed)
		{
			_logger.LogWarning(
				"Could not read managing area during reconnect reconciliation: {Errors}",
				string.Join("; ", managingAreaResult.Errors.Select(e => e.Message)));
			var (pushedRecipe, pushedValid) = NotifyLocalRecipe();
			return BuildResult(PlcReconciliationOutcome.ManagingAreaReadFailed, pushedRecipe, pushedValid, null, null);
		}

		var committed = managingAreaResult.Value.Committed;

		if (!committed)
		{
			var (pushedRecipe, pushedValid) = NotifyLocalRecipe();
			return BuildResult(PlcReconciliationOutcome.NotCommitted, pushedRecipe, pushedValid, null, committed);
		}

		return await CompareWithPlcRecipeAsync(committed, cancellationToken);
	}

	private async Task<PlcReconciliationResult> CompareWithPlcRecipeAsync(
		bool committed,
		CancellationToken cancellationToken)
	{
		var plcRecipeResult = await _reader.ReadRecipeFromPlcAsync(cancellationToken);

		if (plcRecipeResult.IsFailed)
		{
			_logger.LogWarning(
				"Could not read PLC recipe during reconciliation: {Errors}",
				string.Join("; ", plcRecipeResult.Errors.Select(e => e.Message)));
			var (pushedRecipe, pushedValid) = NotifyLocalRecipe();
			return BuildResult(PlcReconciliationOutcome.RecipeReadFailed, pushedRecipe, pushedValid, null, committed);
		}

		var plcRecipe = plcRecipeResult.Value;

		// Both fields are read before the decision, and the pair is still not atomic: a UI-thread
		// mutation between the two reads pairs the step count of one recipe with the validity of another.
		var localRecipe = _session.Current;
		var localValid = _session.IsValid;

		return await ResolveRecipeDifferenceAsync(localRecipe, localValid, plcRecipe, committed);
	}

	private async Task<PlcReconciliationResult> ResolveRecipeDifferenceAsync(
		Recipe localRecipe,
		bool localValid,
		Recipe plcRecipe,
		bool committed)
	{
		var plcStepCount = plcRecipe.Steps.Count;

		if (localRecipe.Steps.Count == 0 && plcStepCount > 0)
		{
			var applied = await ApplyReconnectPlcRecipeAsync(plcRecipe);

			// The snapshot, not the session: a successful apply has already replaced the session recipe
			// with the PLC one, and logging that would report the PLC recipe as the local side.
			return BuildResult(
				applied ? PlcReconciliationOutcome.AppliedFromPlc : PlcReconciliationOutcome.ApplyFromPlcFailed,
				localRecipe,
				localValid,
				plcStepCount,
				committed);
		}

		if (plcStepCount > 0 && !localRecipe.Equals(plcRecipe))
		{
			lock (_pendingLock)
			{
				_pendingPlcRecipe = plcRecipe;
			}

			PlcRecipeConflictDetected?.Invoke(localRecipe, plcRecipe);

			return BuildResult(
				PlcReconciliationOutcome.ConflictRaised, localRecipe, localValid, plcStepCount, committed);
		}

		var (pushedRecipe, pushedValid) = NotifyLocalRecipe();

		return BuildResult(
			plcStepCount == 0 ? PlcReconciliationOutcome.PlcEmpty : PlcReconciliationOutcome.Identical,
			pushedRecipe,
			pushedValid,
			plcStepCount,
			committed);
	}

	private PlcReconciliationResult BuildResultFromSession(PlcReconciliationOutcome outcome)
	{
		return BuildResult(outcome, _session.Current, _session.IsValid, null, null);
	}

	private PlcReconciliationResult BuildResult(
		PlcReconciliationOutcome outcome,
		Recipe localRecipe,
		bool localValid,
		int? plcStepCount,
		bool? committed)
	{
		return new PlcReconciliationResult(
			outcome,
			localRecipe.Steps.Count,
			localValid,
			plcStepCount,
			committed,
			PlcRecipeConflictDetected is not null);
	}

	private void LogOutcome(PlcReconciliationResult result, Exception? exception = null)
	{
		var level = ResolveOutcomeLevel(result.Outcome);

		if (result.PlcStepCount is null && result.Committed is null)
		{
			_logger.Log(
				level,
				exception,
				ShortOutcomeTemplate,
				result.Outcome,
				result.LocalStepCount,
				result.LocalValid);
			return;
		}

		_logger.Log(
			level,
			exception,
			LongOutcomeTemplate,
			result.Outcome,
			result.LocalStepCount,
			result.LocalValid,
			result.PlcStepCount,
			result.Committed,
			result.HasConflictSubscriber);
	}

	private static LogLevel ResolveOutcomeLevel(PlcReconciliationOutcome outcome)
	{
		return outcome switch
		{
			PlcReconciliationOutcome.AppliedFromPlc => LogLevel.Information,
			PlcReconciliationOutcome.ConflictRaised => LogLevel.Information,
			PlcReconciliationOutcome.PlcEmpty => LogLevel.Information,
			PlcReconciliationOutcome.Identical => LogLevel.Information,
			PlcReconciliationOutcome.Cancelled => LogLevel.Information,
			PlcReconciliationOutcome.ProtocolVersionMismatch => LogLevel.Warning,
			PlcReconciliationOutcome.ProtocolVersionCheckFailed => LogLevel.Warning,
			PlcReconciliationOutcome.ManagingAreaReadFailed => LogLevel.Warning,
			PlcReconciliationOutcome.NotCommitted => LogLevel.Warning,
			PlcReconciliationOutcome.RecipeReadFailed => LogLevel.Warning,
			PlcReconciliationOutcome.ApplyFromPlcFailed => LogLevel.Warning,
			PlcReconciliationOutcome.TimedOut => LogLevel.Warning,
			_ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unnamed reconciliation outcome")
		};
	}

	private async Task<bool> ApplyReconnectPlcRecipeAsync(Recipe plcRecipe)
	{
		var callback = _reconnectApplyCallback;
		if (callback is null)
		{
			_logger.LogWarning(
				"Reconnect reconciliation discarded PLC recipe: no apply callback registered.");
			return false;
		}

		Result applyResult;
		try
		{
			applyResult = await callback(plcRecipe);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Reconnect apply callback threw");
			return false;
		}

		if (applyResult.IsFailed)
		{
			_logger.LogWarning(
				"PLC recipe analysis errors during reconnect: {Errors}",
				string.Join("; ", applyResult.Errors.Select(e => e.Message)));
			return false;
		}

		return true;
	}

	/// <summary>
	/// The UI thread can replace the session recipe at any moment, so a caller that read the session
	/// again to log what it pushed would name a different recipe than the one the executor received.
	/// </summary>
	private (Recipe Recipe, bool Valid) NotifyLocalRecipe()
	{
		var localRecipe = _session.Current;
		var localValid = _session.IsValid;
		_syncService.NotifyRecipeChanged(localRecipe, localValid);

		return (localRecipe, localValid);
	}
}
