using System.Reactive.Linq;
using System.Reactive.Subjects;

using FluentResults;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;

using UI.MessageService;

namespace UI.Coordinator;

public sealed class RecipeMutationCoordinator : IDisposable
{
	private readonly AppConfiguration _appConfiguration;
	private readonly CsvService _csvService;
	private readonly RecipeEditor _editor;
	private readonly ImportedRecipeValidator _importedRecipeValidator;
	private readonly ILogger<RecipeMutationCoordinator> _logger;
	private readonly MessagePanelViewModel _messagePanel;
	private readonly PlcLifecycleManager _plc;
	private readonly Subject<(Recipe Local, Recipe Plc)> _plcRecipeConflictDetected = new();
	private readonly Subject<Result<PlcSessionSnapshot>> _plcStateChanged = new();
	private readonly RecipeQueryService _queryService;
	private readonly Subject<MutationSignal> _stateChanged = new();
	private readonly RecipeStepCoordinator _stepCoordinator;
	private readonly RecipeWorkspace _workspace;
	private bool _disposed;
	private bool _initialized;
	private Result<PlcSessionSnapshot> _lastPlcState = PlcSessionSnapshot.InitialState;
	private Result _lastRecipeResult = Result.Ok();
	private IDisposable? _plcStateSubscription;

	public RecipeMutationCoordinator(
		RecipeWorkspace workspace,
		RecipeEditor editor,
		PlcLifecycleManager plc,
		CsvService csvService,
		ImportedRecipeValidator importedRecipeValidator,
		AppConfiguration appConfiguration,
		RecipeQueryService queryService,
		MessagePanelViewModel messagePanel,
		ILogger<RecipeMutationCoordinator> logger)
	{
		_workspace = workspace;
		_editor = editor;
		_plc = plc;
		_csvService = csvService;
		_importedRecipeValidator = importedRecipeValidator;
		_appConfiguration = appConfiguration;
		_queryService = queryService;
		_messagePanel = messagePanel;
		_logger = logger;
		_stepCoordinator = new RecipeStepCoordinator(
			workspace,
			editor,
			() => _queryService.CurrentRecipe,
			result => _lastRecipeResult = result,
			index => SuggestedSelection = index,
			signal => _stateChanged.OnNext(signal),
			RebuildMessagePanel);
	}

	public IObservable<MutationSignal> StateChanged => _stateChanged;
	public IObservable<(Recipe Local, Recipe Plc)> PlcRecipeConflictDetected => _plcRecipeConflictDetected;
	public IObservable<Result<PlcSessionSnapshot>> PlcStateChanged => _plcStateChanged;

	public int? SuggestedSelection { get; private set; }

	public Recipe CurrentRecipe => _queryService.CurrentRecipe;

	public RecipeSnapshot Snapshot => _queryService.Snapshot;

	public bool IsDirty => _queryService.IsDirty;
	public bool CanUndo => _queryService.CanUndo;
	public bool CanRedo => _queryService.CanRedo;
	public bool IsConnected => _queryService.IsConnected;

	public IObservable<PlcExecutionInfo> ExecutionState => _queryService.ExecutionState;
	public bool IsRecipeActive => _queryService.IsRecipeActive;
	public bool IsSyncEnabled => _queryService.IsSyncEnabled;

	public RecipeQueryService QueryService => _queryService;

	public RecipeMutationCoordinator Initialize()
	{
		if (_initialized)
		{
			throw new InvalidOperationException("RecipeMutationCoordinator has already been initialized.");
		}

		_initialized = true;

		_plc.PlcRecipeConflictDetected += OnPlcRecipeConflictDetected;
		_plc.Initialize();

		_plcStateSubscription = _plc.PlcState
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(OnPlcStateChanged);

		return this;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_plc.PlcRecipeConflictDetected -= OnPlcRecipeConflictDetected;

		_plcStateSubscription?.Dispose();

		_stateChanged.Dispose();
		_plcRecipeConflictDetected.Dispose();
		_plcStateChanged.Dispose();
	}

	public int? ConsumeSuggestedSelection()
	{
		var value = SuggestedSelection;
		SuggestedSelection = null;

		return value;
	}

	public Task<Result> EnableSync()
	{
		return _plc.EnableSync(_appConfiguration.PlcConfiguration);
	}

	public Task DisableSync()
	{
		return _plc.DisableSync();
	}

	public async Task<Result> LoadRecipeFromPlcAsync()
	{
		var result = await _plc.LoadRecipeFromPlcAsync();

		_lastRecipeResult = result;

		RebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		SuggestedSelection = null;
		_stateChanged.OnNext(new MutationSignal.RecipeReplaced());

		return result;
	}

	public Result ResolveConflict(bool keepLocal)
	{
		var result = _plc.ResolveConflict(keepLocal);

		if (!keepLocal && result.IsSuccess)
		{
			_lastRecipeResult = result;
			SuggestedSelection = null;
			_stateChanged.OnNext(new MutationSignal.RecipeReplaced());
		}

		RebuildMessagePanel();

		return result;
	}

	public Result AppendStep(int actionId)
	{
		return _stepCoordinator.AppendStep(actionId);
	}

	public Result InsertStep(int index, int actionId)
	{
		return _stepCoordinator.InsertStep(index, actionId);
	}

	public Result RemoveStep(int index)
	{
		return _stepCoordinator.RemoveStep(index);
	}

	public Result RemoveSteps(IReadOnlyList<int> indices)
	{
		return _stepCoordinator.RemoveSteps(indices);
	}

	public Result InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		return _stepCoordinator.InsertSteps(startIndex, steps);
	}

	public Result ChangeStepAction(int stepIndex, int newActionId)
	{
		return _stepCoordinator.ChangeStepAction(stepIndex, newActionId);
	}

	public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		return _stepCoordinator.UpdateStepProperty(stepIndex, columnKey, value);
	}

	public Result Undo()
	{
		return _stepCoordinator.Undo();
	}

	public Result Redo()
	{
		return _stepCoordinator.Redo();
	}

	public Result NewRecipe()
	{
		return _stepCoordinator.NewRecipe();
	}

	public async Task<Result> LoadRecipeAsync(string filePath)
	{
		var loadResult = await _csvService.LoadAsync(filePath);
		Result result;
		if (loadResult.IsFailed)
		{
			result = loadResult.ToResult();
		}
		else
		{
			result = _workspace.LoadAsCurrentValidated(loadResult.Value, _importedRecipeValidator);
			if (result.IsSuccess)
			{
				_workspace.MarkSaved();
			}
		}

		_lastRecipeResult = result;

		RebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		SuggestedSelection = null;
		_stateChanged.OnNext(new MutationSignal.RecipeReplaced());

		return result;
	}

	public async Task<Result> SaveRecipeAsync(string filePath)
	{
		var result = await _csvService.SaveAsync(_workspace.CurrentRecipe, filePath);

		if (result.IsFailed)
		{
			_logger.LogError("Failed to save recipe to {FilePath}: {Errors}",
				filePath,
				string.Join("; ", result.Errors.Select(e => e.Message)));
		}
		else
		{
			_workspace.MarkSaved();
		}

		RebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		SuggestedSelection = null;
		_stateChanged.OnNext(new MutationSignal.MetadataChanged());

		return result;
	}

	private void OnPlcStateChanged(Result<PlcSessionSnapshot> result)
	{
		if (_disposed)
		{
			return;
		}

		_lastPlcState = result;
		_plcStateChanged.OnNext(result);
		RebuildMessagePanel();
	}

	private void OnPlcRecipeConflictDetected(Recipe local, Recipe plc)
	{
		if (_disposed)
		{
			return;
		}

		_plcRecipeConflictDetected.OnNext((local, plc));
	}

	private void RebuildMessagePanel()
	{
		var combinedReasons = _lastRecipeResult.Reasons.Concat(_lastPlcState.Reasons);
		_messagePanel.RefreshReasons(combinedReasons);
	}
}
