using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Avalonia.Threading;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeGrid.Transposed;

public class TransposedRecipeGridSurface : ReactiveObject, IRecipeGridSurface
{
	private readonly ObservableAsPropertyHelper<bool> _isReadOnly;
	private readonly ObservableAsPropertyHelper<int> _selectedStepIndex;
	private readonly ChangedCellClickAwayBroadcaster _changedCellClickAwayBroadcaster;
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly TransposedExecutionHighlightTracker _executionHighlightTracker;
	private readonly ILogger<TransposedRecipeGridSurface> _logger;
	private readonly MessagePanelViewModel _messagePanel;
	private readonly ParameterCellViewModelFactory _parameterCellViewModelFactory;
	private readonly RecipeMetadataRegistry _recipeMetadataRegistry;
	private readonly Subject<int?> _selectionRequests = new();

	private IReadOnlyList<int> _selectedStepIndices = [];

	public TransposedRecipeGridSurface(
		RecipeCoordinator coordinator,
		RecipeMetadataRegistry recipeMetadataRegistry,
		GridStyleOptions gridStyle,
		MessagePanelViewModel messagePanel,
		ChangedCellClickAwayBroadcaster changedCellClickAwayBroadcaster,
		ILogger<TransposedRecipeGridSurface> logger)
	{
		_coordinator = coordinator;
		_recipeMetadataRegistry = recipeMetadataRegistry;
		GridStyle = gridStyle;
		_messagePanel = messagePanel;
		_changedCellClickAwayBroadcaster = changedCellClickAwayBroadcaster;
		_logger = logger;

		ParameterDescriptors = ParameterDescriptor.BuildFromRegistry(recipeMetadataRegistry);
		_parameterCellViewModelFactory = new ParameterCellViewModelFactory(recipeMetadataRegistry);
		StepColumns = new ObservableCollection<StepColumnViewModel>();
		_executionHighlightTracker = new TransposedExecutionHighlightTracker(StepColumns);

		CanDeleteStep = this
			.WhenAnyValue(x => x.SelectedStepIndices)
			.Select(indices => indices.Count > 0)
			.DistinctUntilChanged();

		_selectedStepIndex = this
			.WhenAnyValue(x => x.SelectedStepIndices)
			.Select(indices => indices.Count > 0 ? indices[0] : -1)
			.ToProperty(this, x => x.SelectedStepIndex)
			.DisposeWith(_disposables);

		_isReadOnly = coordinator.CanEditRecipe
			.Select(canEdit => !canEdit)
			.ToProperty(this, x => x.IsReadOnly)
			.DisposeWith(_disposables);

		EditorMustClose = this
			.WhenAnyValue(x => x.IsReadOnly)
			.Skip(1)
			.Where(readOnly => readOnly)
			.Select(_ => Unit.Default);

		coordinator.ExecutionState
			.Subscribe(_executionHighlightTracker.OnExecutionStateChanged)
			.DisposeWith(_disposables);

		coordinator.Mutated += OnMutation;
		Disposable.Create(() => coordinator.Mutated -= OnMutation)
			.DisposeWith(_disposables);

		changedCellClickAwayBroadcaster.Cleared += OnChangedCellClickAwayCleared;
		Disposable.Create(() => changedCellClickAwayBroadcaster.Cleared -= OnChangedCellClickAwayCleared)
			.DisposeWith(_disposables);
	}

	public IObservable<Unit> EditorMustClose { get; }

	public GridStyleOptions GridStyle { get; }

	public IReadOnlyList<ParameterDescriptor> ParameterDescriptors { get; }

	public ObservableCollection<StepColumnViewModel> StepColumns { get; }

	public IObservable<bool> CanDeleteStep { get; }

	public bool IsReadOnly => _isReadOnly.Value;

	public int SelectedStepIndex => _selectedStepIndex.Value;

	public IReadOnlyList<int> SelectedStepIndices
	{
		get => _selectedStepIndices;
		private set => this.RaiseAndSetIfChanged(ref _selectedStepIndices, value);
	}

	public int StepCount => StepColumns.Count;

	public IObservable<int?> SelectionRequests => _selectionRequests.AsObservable();

	public void UpdateSelection(IReadOnlyList<int> stepIndices)
	{
		SelectedStepIndices = stepIndices;
	}

	public void Dispose()
	{
		DisposeAllColumns();
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	public void Initialize()
	{
		FullRebuild(_coordinator.CurrentRecipe);
	}

	public void OnMutation(MutationSignal signal)
	{
		Dispatcher.UIThread.VerifyAccess();

		var recipe = _coordinator.CurrentRecipe;

		_logger.LogInformation(
			"Mutation signal received: {Kind} StepCount={StepCount}",
			signal.GetType().Name,
			recipe.StepCount);

		switch (signal)
		{
			case MutationSignal.PropertyUpdated(var stepIndex):
				UpdateSingleColumnInPlace(recipe, stepIndex);
				break;

			case MutationSignal.StepAppended(var index):
				AppendColumn(recipe, index);
				break;

			case MutationSignal.StepsInserted(var startIndex, var count):
				InsertColumns(recipe, startIndex, count);
				break;

			case MutationSignal.StepRemoved(var removedIndex):
				RemoveColumn(removedIndex);
				break;

			case MutationSignal.StepsRemoved(var removedIndices):
				RemoveColumns(removedIndices);
				break;

			case MutationSignal.StepActionChanged(var stepIndex):
				RebuildColumn(recipe, stepIndex);
				break;

			case MutationSignal.RecipeReplaced:
				FullRebuild(recipe);
				break;

			case MutationSignal.StateRefreshed:
				return;
		}

		ReconcileSelectionWithColumns();
		RefreshStepStartTimes();
		RefreshColumnLoopDepths();
	}

	private void ReconcileSelectionWithColumns()
	{
		var currentSelection = SelectedStepIndices;
		if (currentSelection.Count == 0)
		{
			return;
		}

		var columnCount = StepColumns.Count;
		if (currentSelection.All(index => index < columnCount))
		{
			return;
		}

		SelectedStepIndices = currentSelection.Where(index => index < columnCount).ToList();
	}

	public void RequestSelection(int? stepIndex)
	{
		if (_disposables.IsDisposed)
		{
			return;
		}

		_selectionRequests.OnNext(stepIndex);
	}

	// A click-away acknowledgement must land on both orientation surfaces (each holds its own
	// row view model for the step), so the clear round-trips through the shared broadcaster
	// instead of touching this surface's row directly. Rows no longer in the projection are
	// skipped (the view arms the pending cell before mutations can replace its row).
	public void ClearChangedByClickAway(RecipeRowViewModel row, string columnKey)
	{
		for (var i = 0; i < StepColumns.Count; i++)
		{
			if (ReferenceEquals(StepColumns[i].Row, row))
			{
				_changedCellClickAwayBroadcaster.Publish(i, columnKey);
				return;
			}
		}
	}

	private void OnChangedCellClickAwayCleared(int stepIndex, string columnKey)
	{
		if (stepIndex >= 0 && stepIndex < StepColumns.Count)
		{
			StepColumns[stepIndex].Row.ClearChanged(columnKey);
		}
	}

	private void OnCellValueChanged(StepColumnViewModel stepColumn, string columnKey, string? value)
	{
		if (value is null)
		{
			return;
		}

		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = StepColumns.IndexOf(stepColumn);
		if (stepIndex < 0)
		{
			return;
		}

		var result = _coordinator.UpdateStepProperty(stepIndex, columnKey, value);

		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result, $"Step {stepIndex + 1}");
			return;
		}

		stepColumn.Row.ClearChanged(columnKey);
	}

	private void OnSelectorValueChanged(StepColumnViewModel stepColumn, SelectorEdit selectorEdit)
	{
		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = StepColumns.IndexOf(stepColumn);
		if (stepIndex < 0)
		{
			return;
		}

		var result = _coordinator.UpdateStepForSelectorChange(
			stepIndex,
			selectorEdit.SelectorKey,
			selectorEdit.Value,
			selectorEdit.ColumnsToDrop,
			selectorEdit.ColumnsToSeed);

		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result, $"Step {stepIndex + 1}");
			return;
		}

		stepColumn.Row.RecomputeInapplicableColumns();
		stepColumn.Row.ApplyChangedDelta(add: selectorEdit.ColumnsToSeed, remove: selectorEdit.ColumnsToDrop);
	}

	private void OnActionChanged(StepColumnViewModel stepColumn, int newActionId)
	{
		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = StepColumns.IndexOf(stepColumn);
		if (stepIndex < 0)
		{
			return;
		}

		var result = _coordinator.ChangeStepAction(stepIndex, newActionId);

		if (result.IsFailed)
		{
			_messagePanel.ReportError(
				$"Step {stepIndex + 1}: Failed to change action - {result.FormatErrors()}");
			return;
		}

		RequestSelection(result.Value);
	}

	private void UpdateSingleColumnInPlace(Recipe recipe, int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= recipe.StepCount)
		{
			_logger.LogWarning(
				"Stale PropertyUpdated signal dropped: stepIndex={StepIndex} out of recipe range (StepCount={StepCount})",
				stepIndex,
				recipe.StepCount);
			return;
		}

		if (stepIndex >= StepColumns.Count)
		{
			_logger.LogWarning(
				"Stale PropertyUpdated signal dropped: stepIndex={StepIndex} exceeds StepColumns.Count={ColumnCount}",
				stepIndex,
				StepColumns.Count);
			return;
		}

		RecipeRowUpdateSynchronizer.ApplyPropertyUpdate(StepColumns[stepIndex].Row, recipe.Steps[stepIndex]);
	}

	private void AppendColumn(Recipe recipe, int index)
	{
		if (index < 0 || index >= recipe.StepCount)
		{
			_logger.LogWarning(
				"Stale StepAppended signal dropped: index={Index} out of recipe range (StepCount={StepCount})",
				index,
				recipe.StepCount);
			return;
		}

		var stepColumn = TryCreateColumn(recipe.Steps[index], index + 1);
		if (stepColumn is null)
		{
			return;
		}
		StepColumns.Add(stepColumn);
	}

	private void InsertColumns(Recipe recipe, int startIndex, int count)
	{
		if (startIndex < 0 || count <= 0 || startIndex + count > recipe.StepCount)
		{
			_logger.LogWarning(
				"Stale StepsInserted signal dropped: startIndex={StartIndex}, count={Count}, recipe StepCount={StepCount}",
				startIndex,
				count,
				recipe.StepCount);
			return;
		}

		if (startIndex > StepColumns.Count)
		{
			_logger.LogWarning(
				"Stale StepsInserted signal dropped: startIndex={StartIndex} exceeds StepColumns.Count={ColumnCount}",
				startIndex,
				StepColumns.Count);
			return;
		}

		for (var i = 0; i < count; i++)
		{
			var index = startIndex + i;
			var stepColumn = TryCreateColumn(recipe.Steps[index], index + 1);
			if (stepColumn is null)
			{
				continue;
			}
			StepColumns.Insert(index, stepColumn);
		}

		RenumberColumns(startIndex + count);
	}

	private void RemoveColumn(int removedIndex)
	{
		if (removedIndex < 0 || removedIndex >= StepColumns.Count)
		{
			_logger.LogWarning(
				"Stale StepRemoved signal dropped: removedIndex={RemovedIndex} out of StepColumns range (Count={ColumnCount})",
				removedIndex,
				StepColumns.Count);
			return;
		}

		StepColumns[removedIndex].Dispose();
		StepColumns.RemoveAt(removedIndex);
		RenumberColumns(removedIndex);
	}

	private void RemoveColumns(IReadOnlyList<int> removedIndices)
	{
		if (removedIndices.Count == 0)
		{
			return;
		}

		foreach (var index in removedIndices)
		{
			if (index < 0 || index >= StepColumns.Count)
			{
				_logger.LogWarning(
					"Stale StepsRemoved signal dropped: index={Index} out of StepColumns range (Count={ColumnCount})",
					index,
					StepColumns.Count);
				return;
			}
		}

		foreach (var index in removedIndices.OrderByDescending(i => i))
		{
			StepColumns[index].Dispose();
			StepColumns.RemoveAt(index);
		}

		var minIndex = removedIndices.Min();
		RenumberColumns(minIndex);
	}

	private void RebuildColumn(Recipe recipe, int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= recipe.StepCount || stepIndex >= StepColumns.Count)
		{
			_logger.LogWarning(
				"Stale StepActionChanged signal dropped: stepIndex={StepIndex}, recipe StepCount={StepCount}, StepColumns.Count={ColumnCount}",
				stepIndex,
				recipe.StepCount,
				StepColumns.Count);
			return;
		}

		var step = recipe.Steps[stepIndex];
		var stepColumn = TryCreateColumn(step, stepIndex + 1);
		if (stepColumn is null)
		{
			return;
		}
		StepColumns[stepIndex].Dispose();
		StepColumns[stepIndex] = stepColumn;

		stepColumn.Row.MarkChanged(step.Properties.Keys.Select(id => id.Value));
	}

	private void RenumberColumns(int fromIndex)
	{
		for (var i = fromIndex; i < StepColumns.Count; i++)
		{
			StepColumns[i].Row.UpdateStepNumber(i + 1);
		}
	}

	private void FullRebuild(Recipe recipe)
	{
		_executionHighlightTracker.Reset();

		DisposeAllColumns();
		StepColumns.Clear();

		for (var i = 0; i < recipe.StepCount; i++)
		{
			var stepColumn = TryCreateColumn(recipe.Steps[i], i + 1);
			if (stepColumn is null)
			{
				continue;
			}
			StepColumns.Add(stepColumn);
		}
	}

	private void RefreshStepStartTimes()
	{
		var stepStartTimes = _coordinator.Snapshot.StepStartTimes;
		for (var i = 0; i < StepColumns.Count; i++)
		{
			string formattedTime;
			if (stepStartTimes.TryGetValue(i, out var time))
			{
				var rawSeconds = time.TotalSeconds.ToString(CultureInfo.InvariantCulture);
				formattedTime = TimeFormatHelper.FormatValue(
					rawSeconds,
					TimeFormatHelper.TimeHmsFormat,
					TimeFormatHelper.TimeUnits);
			}
			else
			{
				formattedTime = string.Empty;
			}

			StepColumns[i].Row.UpdateStepStartTime(formattedTime);
		}
	}

	private void RefreshColumnLoopDepths()
	{
		var rowLoopDepths = _coordinator.Snapshot.RowLoopDepths;
		for (var i = 0; i < StepColumns.Count; i++)
		{
			StepColumns[i].Row.ForDepth = Math.Min(rowLoopDepths[i], 3);
		}
	}

	public IReadOnlyList<Step> CollectSelectedSteps()
	{
		var recipe = _coordinator.CurrentRecipe;

		return _selectedStepIndices
			.OrderBy(i => i)
			.Select(i => recipe.Steps[i])
			.ToList();
	}

	private StepColumnViewModel? TryCreateColumn(Step step, int stepNumber)
	{
		var actionResult = _recipeMetadataRegistry.GetAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			_logger.LogError(
				"Unknown action key {ActionKey} in step {StepNumber}: {Errors}",
				step.ActionKey,
				stepNumber,
				actionResult.FormatErrors());
			_messagePanel.ReportError($"Step {stepNumber}: unknown action (key={step.ActionKey})");
			return null;
		}

		return CreateColumnViewModel(step, actionResult.Value, stepNumber);
	}

	private StepColumnViewModel CreateColumnViewModel(
		Step step,
		ActionDefinition action,
		int stepNumber)
	{
		var stepColumn = new StepColumnViewModel(
			stepNumber,
			step,
			action,
			_recipeMetadataRegistry,
			ParameterDescriptors,
			_parameterCellViewModelFactory.Create);

		stepColumn.Row.PropertyValueChanged +=
			(columnKey, value) => OnCellValueChanged(stepColumn, columnKey, value);
		stepColumn.Row.ActionChanged += actionId => OnActionChanged(stepColumn, actionId);
		stepColumn.Row.SelectorValueChanged += selectorEdit => OnSelectorValueChanged(stepColumn, selectorEdit);

		return stepColumn;
	}

	private void DisposeAllColumns()
	{
		foreach (var stepColumn in StepColumns)
		{
			stepColumn.Dispose();
		}
	}
}
