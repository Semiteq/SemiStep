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

using SemiStep.Core.Recipes;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeGrid;

public class CanonicalRecipeGridSurface : ReactiveObject, IRecipeGridSurface
{
	private readonly ObservableAsPropertyHelper<bool> _isReadOnly;
	private readonly ObservableAsPropertyHelper<int> _selectedStepIndex;
	private readonly ChangedCellClickAwayBroadcaster _changedCellClickAwayBroadcaster;
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly ExecutionHighlightTracker _executionHighlightTracker;
	private readonly ILogger<CanonicalRecipeGridSurface> _logger;
	private readonly MessagePanelViewModel _messagePanel;
	private readonly Subject<int?> _selectionRequests = new();

	private IReadOnlyList<int> _selectedStepIndices = [];

	public CanonicalRecipeGridSurface(
		RecipeCoordinator coordinator,
		RecipeMetadataRegistry recipeMetadataRegistry,
		ColumnBuilder columnBuilder,
		MessagePanelViewModel messagePanel,
		ChangedCellClickAwayBroadcaster changedCellClickAwayBroadcaster,
		ILogger<CanonicalRecipeGridSurface> logger)
	{
		_coordinator = coordinator;
		RecipeMetadataRegistry = recipeMetadataRegistry;
		ColumnBuilder = columnBuilder;
		_messagePanel = messagePanel;
		_changedCellClickAwayBroadcaster = changedCellClickAwayBroadcaster;
		_logger = logger;

		RecipeRows = new ObservableCollection<RecipeRowViewModel>();
		_executionHighlightTracker = new ExecutionHighlightTracker(RecipeRows);

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

	internal RecipeMetadataRegistry RecipeMetadataRegistry { get; }

	public ColumnBuilder ColumnBuilder { get; }

	public ObservableCollection<RecipeRowViewModel> RecipeRows { get; }

	public IObservable<bool> CanDeleteStep { get; }

	public bool IsReadOnly => _isReadOnly.Value;

	public int SelectedStepIndex => _selectedStepIndex.Value;

	public IReadOnlyList<int> SelectedStepIndices
	{
		get => _selectedStepIndices;
		private set => this.RaiseAndSetIfChanged(ref _selectedStepIndices, value);
	}

	public int StepCount => RecipeRows.Count;

	public IObservable<int?> SelectionRequests => _selectionRequests.AsObservable();

	public void UpdateSelection(IReadOnlyList<int> stepIndices)
	{
		SelectedStepIndices = stepIndices;
	}

	public void Dispose()
	{
		DisposeAllRows();
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
				UpdateSingleRowInPlace(recipe, stepIndex);
				break;

			case MutationSignal.StepAppended(var index):
				AppendRow(recipe, index);
				break;

			case MutationSignal.StepsInserted(var startIndex, var count):
				InsertRows(recipe, startIndex, count);
				break;

			case MutationSignal.StepRemoved(var removedIndex):
				RemoveRow(removedIndex);
				break;

			case MutationSignal.StepsRemoved(var removedIndices):
				RemoveRows(removedIndices);
				break;

			case MutationSignal.StepActionChanged(var stepIndex):
				RebuildRow(recipe, stepIndex);
				break;

			case MutationSignal.RecipeReplaced:
				FullRebuild(recipe);
				break;

			case MutationSignal.StateRefreshed:
				return;
		}

		ReconcileSelectionWithRows();
		RefreshStepStartTimes();
		RefreshRowLoopDepths();
	}

	private void ReconcileSelectionWithRows()
	{
		var currentSelection = SelectedStepIndices;
		if (currentSelection.Count == 0)
		{
			return;
		}

		var rowCount = RecipeRows.Count;
		if (currentSelection.All(index => index < rowCount))
		{
			return;
		}

		SelectedStepIndices = currentSelection.Where(index => index < rowCount).ToList();
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
		var stepIndex = RecipeRows.IndexOf(row);
		if (stepIndex < 0)
		{
			return;
		}

		_changedCellClickAwayBroadcaster.Publish(stepIndex, columnKey);
	}

	private void OnChangedCellClickAwayCleared(int stepIndex, string columnKey)
	{
		if (stepIndex >= 0 && stepIndex < RecipeRows.Count)
		{
			RecipeRows[stepIndex].ClearChanged(columnKey);
		}
	}

	private void OnCellValueChanged(RecipeRowViewModel row, string columnKey, string? value)
	{
		if (value is null)
		{
			return;
		}

		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = RecipeRows.IndexOf(row);
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

		row.ClearChanged(columnKey);
	}

	private void OnSelectorValueChanged(RecipeRowViewModel row, SelectorEdit selectorEdit)
	{
		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = RecipeRows.IndexOf(row);
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

		row.RecomputeInapplicableColumns();
		row.ApplyChangedDelta(add: selectorEdit.ColumnsToSeed, remove: selectorEdit.ColumnsToDrop);
	}

	private void OnActionChanged(RecipeRowViewModel row, int newActionId)
	{
		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = RecipeRows.IndexOf(row);
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

	private void UpdateSingleRowInPlace(Recipe recipe, int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= recipe.StepCount)
		{
			_logger.LogWarning(
				"Stale PropertyUpdated signal dropped: stepIndex={StepIndex} out of recipe range (StepCount={StepCount})",
				stepIndex,
				recipe.StepCount);
			return;
		}

		if (stepIndex >= RecipeRows.Count)
		{
			_logger.LogWarning(
				"Stale PropertyUpdated signal dropped: stepIndex={StepIndex} exceeds RecipeRows.Count={RowCount}",
				stepIndex,
				RecipeRows.Count);
			return;
		}

		RecipeRowUpdateSynchronizer.ApplyPropertyUpdate(RecipeRows[stepIndex], recipe.Steps[stepIndex]);
	}

	private void AppendRow(Recipe recipe, int index)
	{
		if (index < 0 || index >= recipe.StepCount)
		{
			_logger.LogWarning(
				"Stale StepAppended signal dropped: index={Index} out of recipe range (StepCount={StepCount})",
				index,
				recipe.StepCount);
			return;
		}

		var row = TryCreateRow(recipe.Steps[index], index + 1);
		if (row is null)
		{
			return;
		}
		RecipeRows.Add(row);
	}

	private void InsertRows(Recipe recipe, int startIndex, int count)
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

		if (startIndex > RecipeRows.Count)
		{
			_logger.LogWarning(
				"Stale StepsInserted signal dropped: startIndex={StartIndex} exceeds RecipeRows.Count={RowCount}",
				startIndex,
				RecipeRows.Count);
			return;
		}

		for (var i = 0; i < count; i++)
		{
			var index = startIndex + i;
			var row = TryCreateRow(recipe.Steps[index], index + 1);
			if (row is null)
			{
				continue;
			}
			RecipeRows.Insert(index, row);
		}

		RenumberRows(startIndex + count);
	}

	private void RemoveRow(int removedIndex)
	{
		if (removedIndex < 0 || removedIndex >= RecipeRows.Count)
		{
			_logger.LogWarning(
				"Stale StepRemoved signal dropped: removedIndex={RemovedIndex} out of RecipeRows range (Count={RowCount})",
				removedIndex,
				RecipeRows.Count);
			return;
		}

		RecipeRows[removedIndex].Dispose();
		RecipeRows.RemoveAt(removedIndex);
		RenumberRows(removedIndex);
	}

	private void RemoveRows(IReadOnlyList<int> removedIndices)
	{
		if (removedIndices.Count == 0)
		{
			return;
		}

		foreach (var index in removedIndices)
		{
			if (index < 0 || index >= RecipeRows.Count)
			{
				_logger.LogWarning(
					"Stale StepsRemoved signal dropped: index={Index} out of RecipeRows range (Count={RowCount})",
					index,
					RecipeRows.Count);
				return;
			}
		}

		foreach (var index in removedIndices.OrderByDescending(i => i))
		{
			RecipeRows[index].Dispose();
			RecipeRows.RemoveAt(index);
		}

		var minIndex = removedIndices.Min();
		RenumberRows(minIndex);
	}

	private void RebuildRow(Recipe recipe, int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= recipe.StepCount || stepIndex >= RecipeRows.Count)
		{
			_logger.LogWarning(
				"Stale StepActionChanged signal dropped: stepIndex={StepIndex}, recipe StepCount={StepCount}, RecipeRows.Count={RowCount}",
				stepIndex,
				recipe.StepCount,
				RecipeRows.Count);
			return;
		}

		var step = recipe.Steps[stepIndex];
		var row = TryCreateRow(step, stepIndex + 1);
		if (row is null)
		{
			return;
		}
		RecipeRows[stepIndex].Dispose();
		RecipeRows[stepIndex] = row;

		row.MarkChanged(step.Properties.Keys.Select(id => id.Value));
	}

	private void RenumberRows(int fromIndex)
	{
		for (var i = fromIndex; i < RecipeRows.Count; i++)
		{
			RecipeRows[i].UpdateStepNumber(i + 1);
		}
	}

	private void FullRebuild(Recipe recipe)
	{
		_executionHighlightTracker.Reset();

		DisposeAllRows();
		RecipeRows.Clear();

		for (var i = 0; i < recipe.StepCount; i++)
		{
			var row = TryCreateRow(recipe.Steps[i], i + 1);
			if (row is null)
			{
				continue;
			}
			RecipeRows.Add(row);
		}
	}

	private void RefreshStepStartTimes()
	{
		var stepStartTimes = _coordinator.Snapshot.StepStartTimes;
		for (var i = 0; i < RecipeRows.Count; i++)
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

			RecipeRows[i].UpdateStepStartTime(formattedTime);
		}
	}

	private void RefreshRowLoopDepths()
	{
		var rowLoopDepths = _coordinator.Snapshot.RowLoopDepths;
		for (var i = 0; i < RecipeRows.Count; i++)
		{
			RecipeRows[i].ForDepth = Math.Min(rowLoopDepths[i], 3);
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

	private RecipeRowViewModel? TryCreateRow(Step step, int stepNumber)
	{
		var actionResult = RecipeMetadataRegistry.GetAction(step.ActionKey);
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

		return CreateRowViewModel(step, actionResult.Value, stepNumber);
	}

	private RecipeRowViewModel CreateRowViewModel(
		Step step,
		ActionDefinition action,
		int stepNumber)
	{
		var inapplicableColumns = RecipeRowViewModel.BuildInapplicableColumns(action, step, RecipeMetadataRegistry);

		var row = new RecipeRowViewModel(
			stepNumber,
			step,
			action,
			RecipeMetadataRegistry,
			inapplicableColumns);

		row.PropertyValueChanged += (columnKey, value) => OnCellValueChanged(row, columnKey, value);
		row.ActionChanged += actionId => OnActionChanged(row, actionId);
		row.SelectorValueChanged += selectorEdit => OnSelectorValueChanged(row, selectorEdit);

		return row;
	}

	private void DisposeAllRows()
	{
		foreach (var row in RecipeRows)
		{
			row.Dispose();
		}
	}
}
