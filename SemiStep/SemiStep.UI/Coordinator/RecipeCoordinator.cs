using System.Reactive.Linq;
using System.Reactive.Subjects;

using Avalonia.Threading;

using FluentResults;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;

using SemiStep.UI.MessageService;

namespace SemiStep.UI.Coordinator;

public sealed class RecipeCoordinator : IDisposable
{
	private readonly AppConfiguration _appConfiguration;
	private readonly IObservable<bool> _canEditRecipe;
	private readonly IDisposable _canEditRecipeConnection;
	private readonly CsvService _csvService;
	private readonly object _disposeLock = new();
	// PLC channels: hop to MainThreadScheduler once here at the source, expose via
	// Publish().RefCount() so all subscribers share a single ObserveOn. Subscribers
	// (RecipeGridViewModel, MainWindowViewModel, PlcMonitorViewModel) consume these
	// without applying their own ObserveOn.
	private readonly IObservable<PlcExecutionInfo> _executionState;
	private readonly ImportedRecipeValidator _importedRecipeValidator;
	private readonly ILogger<RecipeCoordinator> _logger;
	private readonly MessagePanelViewModel _messagePanel;
	private readonly PlcLifecycleManager _plc;
	private readonly Subject<(Recipe Local, Recipe Plc)> _plcRecipeConflictDetected = new();
	private readonly IObservable<(Recipe Local, Recipe Plc)> _plcRecipeConflictDetectedShared;
	private readonly Subject<Result<PlcSessionSnapshot>> _plcStateChanged = new();
	private readonly IObservable<Result<PlcSessionSnapshot>> _plcStateChangedShared;
	private readonly RecipeMetadataRegistry _recipeMetadataRegistry;
	private readonly RecipeSession _session;
	private bool _disposed;
	private bool _initialized;
	private Result<PlcSessionSnapshot> _lastPlcState = PlcSessionSnapshot.InitialState;
	private Result _lastRecipeResult = Result.Ok();
	private IDisposable? _plcStateSubscription;

	public RecipeCoordinator(
		RecipeSession session,
		PlcLifecycleManager plc,
		CsvService csvService,
		ImportedRecipeValidator importedRecipeValidator,
		AppConfiguration appConfiguration,
		RecipeMetadataRegistry recipeMetadataRegistry,
		MessagePanelViewModel messagePanel,
		ILogger<RecipeCoordinator> logger)
	{
		_session = session;
		_plc = plc;
		_csvService = csvService;
		_importedRecipeValidator = importedRecipeValidator;
		_appConfiguration = appConfiguration;
		_recipeMetadataRegistry = recipeMetadataRegistry;
		_messagePanel = messagePanel;
		_logger = logger;

		_executionState = _plc.ExecutionState
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Publish()
			.RefCount();
		_plcStateChangedShared = _plcStateChanged
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Publish()
			.RefCount();
		_plcRecipeConflictDetectedShared = _plcRecipeConflictDetected
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Publish()
			.RefCount();

		var canEditConnectable = _plcStateChangedShared
			.Select(plcState => plcState.IsSuccess ? !plcState.Value.IsSyncEnabled : !IsSyncEnabled)
			.StartWith(!IsSyncEnabled)
			.DistinctUntilChanged()
			.Replay(1);
		_canEditRecipeConnection = canEditConnectable.Connect();
		_canEditRecipe = canEditConnectable;
	}

	public IObservable<(Recipe Local, Recipe Plc)> PlcRecipeConflictDetected => _plcRecipeConflictDetectedShared;
	public IObservable<Result<PlcSessionSnapshot>> PlcStateChanged => _plcStateChangedShared;
	public IObservable<bool> CanEditRecipe => _canEditRecipe;

	public event Action<MutationSignal>? Mutated;

	public Recipe CurrentRecipe => _session.Current;

	public RecipeSnapshot Snapshot => _session.Snapshot.IsSuccess
		? _session.Snapshot.Value
		: RecipeSnapshot.Empty;

	public bool IsDirty => _session.IsDirty;
	public bool CanUndo => _session.CanUndo;
	public bool CanRedo => _session.CanRedo;

	public bool IsConnected => _plc.IsConnected;
	public bool IsRecipeActive => _plc.IsRecipeActive;
	public bool IsSyncEnabled => _plc.IsSyncEnabled;
	public IObservable<PlcExecutionInfo> ExecutionState => _executionState;
	public PlcSyncStatus SyncStatus => _plc.SyncStatus;
	public DateTimeOffset? LastSyncTime => _plc.LastSyncTime;

	public RecipeCoordinator Initialize()
	{
		if (_initialized)
		{
			throw new InvalidOperationException("RecipeCoordinator has already been initialized.");
		}

		_plc.PlcRecipeConflictDetected += OnPlcRecipeConflictDetected;
		_plc.RegisterReconnectApplyCallback(ApplyReconnectPlcRecipeAsync);

		try
		{
			_plc.Initialize();

			_plcStateSubscription = _plc.PlcState
				.ObserveOn(RxSchedulers.MainThreadScheduler)
				.Subscribe(OnPlcStateChanged);
		}
		catch
		{
			_plc.PlcRecipeConflictDetected -= OnPlcRecipeConflictDetected;
			throw;
		}

		_initialized = true;

		return this;
	}

	public void Dispose()
	{
		lock (_disposeLock)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			_plc.PlcRecipeConflictDetected -= OnPlcRecipeConflictDetected;

			_plcStateSubscription?.Dispose();
			_canEditRecipeConnection.Dispose();

			_plcRecipeConflictDetected.Dispose();
			_plcStateChanged.Dispose();
		}
	}

	public int GetDefaultActionId()
	{
		return _recipeMetadataRegistry.GetAllActions().FirstOrDefault()?.Id
			?? throw new InvalidOperationException("No actions are defined in the configuration.");
	}

	public async Task<Result> EnableSync()
	{
		_logger.LogInformation("PLC sync enable requested");
		var result = await _plc.EnableSync(_appConfiguration.PlcConfiguration);
		if (result.IsFailed)
		{
			_logger.LogWarning(
				"PLC sync enable failed: {Errors}",
				string.Join("; ", result.Errors.Select(e => e.Message)));
		}
		else
		{
			_logger.LogInformation("PLC sync enabled");
		}

		return result;
	}

	public async Task DisableSync()
	{
		_logger.LogInformation("PLC sync disable requested");
		await _plc.DisableSync();
		_logger.LogInformation("PLC sync disabled");
	}

	public async Task<Result> LoadRecipeFromPlcAsync()
	{
		_logger.LogInformation("Loading recipe from PLC");

		var readResult = await _plc.ReadRecipeFromPlcAsync();
		Result result;
		if (readResult.IsFailed)
		{
			result = readResult.ToResult();
		}
		else
		{
			// Marshal the session mutation onto the UI thread (see DispatchMutation).
			result = await Dispatcher.UIThread.InvokeAsync(() => _plc.ApplyRecipeFromPlc(readResult.Value));
		}

		_lastRecipeResult = result;

		if (result.IsFailed)
		{
			RebuildMessagePanel();
			_logger.LogWarning(
				"Failed to load recipe from PLC: {Errors}",
				string.Join("; ", result.Errors.Select(e => e.Message)));
			return result;
		}

		_session.MarkSaved();
		_logger.LogInformation(
			"Loaded recipe from PLC: {StepCount} steps",
			_session.Current.StepCount);

		DispatchMutation(new MutationSignal.RecipeReplaced());
		RebuildMessagePanel();

		return result;
	}

	public Result ResolveConflict(bool keepLocal)
	{
		_logger.LogInformation("PLC recipe conflict resolution: KeepLocal={KeepLocal}", keepLocal);

		var result = _plc.ResolveConflict(keepLocal);

		if (!keepLocal && result.IsSuccess)
		{
			_lastRecipeResult = result;
			DispatchMutation(new MutationSignal.RecipeReplaced());
		}

		RebuildMessagePanel();

		return result;
	}

	public Result<int?> AppendStep(int actionId)
	{
		var result = _session.AppendStep(actionId);
		return Track(result, new MutationSignal.StepAppended(_session.Current.StepCount - 1));
	}

	public Result<int?> InsertStep(int index, int actionId)
	{
		return Track(
			_session.InsertStep(index, actionId),
			new MutationSignal.StepsInserted(index, 1));
	}

	public Result<int?> RemoveStep(int index)
	{
		return Track(
			_session.RemoveStep(index),
			new MutationSignal.StepRemoved(index));
	}

	public Result<int?> RemoveSteps(IReadOnlyList<int> indices)
	{
		return Track(
			_session.RemoveSteps(indices),
			new MutationSignal.StepsRemoved([.. indices]));
	}

	public Result<int?> InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		return Track(
			_session.InsertSteps(startIndex, steps),
			new MutationSignal.StepsInserted(startIndex, steps.Count));
	}

	public Result<int?> ChangeStepAction(int stepIndex, int newActionId)
	{
		return Track(
			_session.ChangeStepAction(stepIndex, newActionId),
			new MutationSignal.StepActionChanged(stepIndex));
	}

	public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		return TrackVoid(
			_session.UpdateStepProperty(stepIndex, columnKey, value),
			new MutationSignal.PropertyUpdated(stepIndex));
	}

	public Result Undo()
	{
		return TrackVoid(_session.Undo(), new MutationSignal.RecipeReplaced());
	}

	public Result Redo()
	{
		return TrackVoid(_session.Redo(), new MutationSignal.RecipeReplaced());
	}

	public Result NewRecipe()
	{
		return TrackVoid(_session.Reset(), new MutationSignal.RecipeReplaced());
	}

	public async Task<Result> LoadRecipeAsync(string filePath)
	{
		_logger.LogInformation("Loading recipe from file: {FilePath}", filePath);

		var loadResult = await _csvService.LoadAsync(filePath);
		Result result;
		if (loadResult.IsFailed)
		{
			result = loadResult.ToResult();
		}
		else
		{
			// Marshal the session mutation onto the UI thread (see DispatchMutation).
			result = await Dispatcher.UIThread.InvokeAsync(() =>
			{
				var validateResult = _session.LoadAsCurrentValidated(loadResult.Value, _importedRecipeValidator);
				if (validateResult.IsSuccess)
				{
					_session.MarkSaved();
				}
				return validateResult;
			});
		}

		_lastRecipeResult = result;

		if (result.IsFailed)
		{
			RebuildMessagePanel();
			_logger.LogWarning(
				"Failed to load recipe from {FilePath}: {Errors}",
				filePath,
				string.Join("; ", result.Errors.Select(e => e.Message)));
			return result;
		}

		_logger.LogInformation(
			"Loaded recipe from {FilePath}: {StepCount} steps",
			filePath,
			_session.Current.StepCount);

		DispatchMutation(new MutationSignal.RecipeReplaced());
		RebuildMessagePanel();

		return result;
	}

	public async Task<Result> SaveRecipeAsync(string filePath)
	{
		_logger.LogInformation(
			"Saving recipe to file: {FilePath}, StepCount={StepCount}",
			filePath,
			_session.Current.StepCount);

		var result = await _csvService.SaveAsync(_session.Current, filePath);

		if (result.IsFailed)
		{
			_logger.LogError("Failed to save recipe to {FilePath}: {Errors}",
				filePath,
				string.Join("; ", result.Errors.Select(e => e.Message)));
		}
		else
		{
			// Marshal the dirty-flag mutation onto the UI thread (see DispatchMutation).
			await Dispatcher.UIThread.InvokeAsync(() => _session.MarkSaved());
			_logger.LogInformation("Saved recipe to {FilePath}", filePath);
			// Not a step-graph mutation; subscribers refresh window-title / IsDirty / status.
			DispatchMutation(new MutationSignal.StateRefreshed());
		}

		_lastRecipeResult = result;

		RebuildMessagePanel();

		return result;
	}

	private async Task<Result> ApplyReconnectPlcRecipeAsync(Recipe recipe)
	{
		// Reconnect reconciliation resumes on a thread-pool continuation. Hop to the UI
		// dispatcher before mutating the session so the grid sink observes the change on
		// the thread it asserts, then dispatch the mutation signal from the same context.
		return await Dispatcher.UIThread.InvokeAsync(() =>
		{
			var applyResult = _plc.ApplyRecipeFromPlc(recipe);
			if (applyResult.IsFailed)
			{
				_lastRecipeResult = applyResult;
				RebuildMessagePanel();
				return applyResult;
			}

			_lastRecipeResult = applyResult;
			_logger.LogInformation(
				"Reconnect reconciliation applied PLC recipe: {StepCount} steps",
				_session.Current.StepCount);

			DispatchMutation(new MutationSignal.RecipeReplaced());
			RebuildMessagePanel();

			return applyResult;
		});
	}

	private Result<int?> Track(Result<MutationOutcome> result, MutationSignal signal)
	{
		_lastRecipeResult = result.ToResult();
		RebuildMessagePanel();

		if (result.IsFailed)
		{
			return result.ToResult<int?>();
		}

		var outcome = result.Value;
		DispatchMutation(signal);
		return Result.Ok(outcome.SuggestedSelectionIndex).WithReasons(result.Reasons);
	}

	private Result TrackVoid(Result result, MutationSignal signal)
	{
		_lastRecipeResult = result;
		RebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		DispatchMutation(signal);
		return result;
	}

	private void DispatchMutation(MutationSignal signal)
	{
		_logger.LogInformation(
			"Mutation dispatched: {Kind} StepCount={StepCount}",
			signal.GetType().Name,
			_session.Current.StepCount);

		// Mutation entry points include async file/PLC paths that may resume off the UI
		// thread. Subscribers (RecipeGridViewModel.OnMutation) assert UI-thread access, so
		// marshal explicitly here instead of relying on captured sync context.
		if (Dispatcher.UIThread.CheckAccess())
		{
			RaiseMutatedSafely(signal);
		}
		else
		{
			Dispatcher.UIThread.Post(() => RaiseMutatedSafely(signal));
		}
	}

	private void RaiseMutatedSafely(MutationSignal signal)
	{
		try
		{
			Mutated?.Invoke(signal);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Mutated event handler threw");
		}
	}

	private void OnPlcStateChanged(Result<PlcSessionSnapshot> result)
	{
		lock (_disposeLock)
		{
			if (_disposed)
			{
				return;
			}

			LogPlcStateChange(result);

			_lastPlcState = result;
			_plcStateChanged.OnNext(result);
			RebuildMessagePanel();
		}
	}

	private void LogPlcStateChange(Result<PlcSessionSnapshot> result)
	{
		if (result.IsFailed)
		{
			_logger.LogWarning(
				"PLC state change: failure {Errors}",
				string.Join("; ", result.Errors.Select(e => e.Message)));
			return;
		}

		var snapshot = result.Value;
		_logger.LogInformation(
			"PLC state change: Connection={ConnectionState}, SyncStatus={SyncStatus}, SyncEnabled={IsSyncEnabled}",
			snapshot.ConnectionState,
			snapshot.SyncStatus,
			snapshot.IsSyncEnabled);
	}

	private void OnPlcRecipeConflictDetected(Recipe local, Recipe plc)
	{
		lock (_disposeLock)
		{
			if (_disposed)
			{
				return;
			}

			_plcRecipeConflictDetected.OnNext((local, plc));
		}
	}

	private void RebuildMessagePanel()
	{
		var combinedReasons = _lastRecipeResult.Reasons.Concat(_lastPlcState.Reasons);
		_messagePanel.RefreshReasons(combinedReasons);
	}
}
