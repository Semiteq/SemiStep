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

public abstract class RecipeGridSurfaceBase<TItem> : ReactiveObject, IRecipeGridSurface
	where TItem : class, IDisposable
{
	private readonly ObservableAsPropertyHelper<bool> _isReadOnly;
	private readonly ObservableAsPropertyHelper<int> _selectedStepIndex;
	private readonly ChangedCellClickAwayBroadcaster _changedCellClickAwayBroadcaster;
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly ExecutionHighlightTracker _executionHighlightTracker;
	private readonly ILogger _logger;
	private readonly MessagePanelViewModel _messagePanel;
	private readonly Subject<int?> _selectionRequests = new();

	private IReadOnlyList<int> _selectedStepIndices = [];

	protected RecipeGridSurfaceBase(
		RecipeCoordinator coordinator,
		RecipeMetadataRegistry recipeMetadataRegistry,
		MessagePanelViewModel messagePanel,
		ChangedCellClickAwayBroadcaster changedCellClickAwayBroadcaster,
		ILogger logger)
	{
		_coordinator = coordinator;
		RecipeMetadataRegistry = recipeMetadataRegistry;
		_messagePanel = messagePanel;
		_changedCellClickAwayBroadcaster = changedCellClickAwayBroadcaster;
		_logger = logger;

		Items = new ObservableCollection<TItem>();
		_executionHighlightTracker = new ExecutionHighlightTracker(() => Items.Count, i => RowOf(Items[i]));

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

	protected ObservableCollection<TItem> Items { get; }

	protected RecipeMetadataRegistry RecipeMetadataRegistry { get; }

	public IObservable<bool> CanDeleteStep { get; }

	public bool IsReadOnly => _isReadOnly.Value;

	public int SelectedStepIndex => _selectedStepIndex.Value;

	public IReadOnlyList<int> SelectedStepIndices
	{
		get => _selectedStepIndices;
		private set => this.RaiseAndSetIfChanged(ref _selectedStepIndices, value);
	}

	public int StepCount => Items.Count;

	public IObservable<int?> SelectionRequests => _selectionRequests.AsObservable();

	protected abstract RecipeRowViewModel RowOf(TItem item);

	protected abstract TItem CreateItem(int stepNumber, Step step, ActionDefinition action);

	public void UpdateSelection(IReadOnlyList<int> stepIndices)
	{
		SelectedStepIndices = stepIndices;
	}

	public void Dispose()
	{
		DisposeAllItems();
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	public void Initialize()
	{
		FullRebuild(_coordinator.CurrentRecipe);

		// FullRebuild installs fresh rows carrying StepStartTime=null and ForDepth=0. Run the tail
		// once from index 0 to establish the incremental start-time baseline; otherwise the first
		// post-init mutation would refresh only from its own index and leave earlier rows blank.
		RefreshStepStartTimes(0);
		RefreshLoopDepths();
	}

	public void OnMutation(MutationSignal signal)
	{
		Dispatcher.UIThread.VerifyAccess();

		var recipe = _coordinator.CurrentRecipe;

		_logger.LogInformation(
			"Mutation signal received: {Kind} StepCount={StepCount}",
			signal.GetType().Name,
			recipe.StepCount);

		// Catching here (not letting the throw escape to RecipeCoordinator.RaiseMutatedSafely)
		// keeps the multicast Mutated invocation intact: the sibling surface and the other
		// subscribers still receive the signal, and the failure stays user-visible.
		try
		{
			switch (signal)
			{
				case MutationSignal.PropertyUpdated(var stepIndex):
					UpdateSingleItemInPlace(recipe, stepIndex);
					break;

				case MutationSignal.StepAppended(var index):
					AppendItem(recipe, index);
					break;

				case MutationSignal.StepsInserted(var startIndex, var count):
					InsertItems(recipe, startIndex, count);
					break;

				case MutationSignal.StepRemoved(var removedIndex):
					RemoveItem(removedIndex);
					break;

				case MutationSignal.StepsRemoved(var removedIndices):
					RemoveItems(removedIndices);
					break;

				case MutationSignal.StepActionChanged(var stepIndex):
					RebuildItem(recipe, stepIndex);
					break;

				case MutationSignal.RecipeReplaced:
					FullRebuild(recipe);
					break;

				case MutationSignal.StateRefreshed:
					return;
			}
		}
		catch (UnknownActionKeyException exception)
		{
			_logger.LogError(
				exception,
				"Projection update failed for {Kind}: unknown action key",
				signal.GetType().Name);
			_messagePanel.ReportError(exception.Message);
			return;
		}

		ReconcileSelectionWithItems();
		RefreshStepStartTimes(RefreshStartIndexFor(signal));
		RefreshLoopDepths();
	}

	// Drives the incremental start-time refresh only. start-time[i] is forward-prefix-determined
	// (computed from steps 0..i-1), so a mutation at index k cannot change any start-time before k;
	// refreshing from this index down is behavior-preserving. Loop-depth is a matched-bracket
	// property that a committed marker mutation can change for rows before k (deleting an EndForLoop
	// unnests its opening marker above it), so RefreshLoopDepths stays a full 0..Count scan and is
	// never driven by this index.
	private static int RefreshStartIndexFor(MutationSignal signal)
	{
		var fromIndex = signal switch
		{
			MutationSignal.PropertyUpdated(var stepIndex) => stepIndex,
			MutationSignal.StepAppended(var index) => index,
			MutationSignal.StepsInserted(var startIndex, _) => startIndex,
			MutationSignal.StepRemoved(var removedIndex) => removedIndex,
			MutationSignal.StepsRemoved(var removedIndices) => removedIndices.Length > 0 ? removedIndices.Min() : 0,
			MutationSignal.StepActionChanged(var stepIndex) => stepIndex,
			_ => 0
		};

		return Math.Max(fromIndex, 0);
	}

	private void ReconcileSelectionWithItems()
	{
		var currentSelection = SelectedStepIndices;
		if (currentSelection.Count == 0)
		{
			return;
		}

		var itemCount = Items.Count;
		if (currentSelection.All(index => index < itemCount))
		{
			return;
		}

		SelectedStepIndices = currentSelection.Where(index => index < itemCount).ToList();
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
		for (var i = 0; i < Items.Count; i++)
		{
			if (ReferenceEquals(RowOf(Items[i]), row))
			{
				_changedCellClickAwayBroadcaster.Publish(i, columnKey);
				return;
			}
		}
	}

	private void OnChangedCellClickAwayCleared(int stepIndex, string columnKey)
	{
		if (stepIndex >= 0 && stepIndex < Items.Count)
		{
			RowOf(Items[stepIndex]).ClearChanged(columnKey);
		}
	}

	private void OnCellValueChanged(TItem item, string columnKey, string? value)
	{
		if (value is null)
		{
			return;
		}

		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = Items.IndexOf(item);
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

		RowOf(item).ClearChanged(columnKey);
	}

	private void OnSelectorValueChanged(TItem item, SelectorEdit selectorEdit)
	{
		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = Items.IndexOf(item);
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

		var row = RowOf(item);
		row.RecomputeInapplicableColumns();
		row.ApplyChangedDelta(add: selectorEdit.ColumnsToSeed, remove: selectorEdit.ColumnsToDrop);
	}

	private void OnActionChanged(TItem item, int newActionId)
	{
		if (IsReadOnly)
		{
			return;
		}

		var stepIndex = Items.IndexOf(item);
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

	private void UpdateSingleItemInPlace(Recipe recipe, int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= recipe.StepCount)
		{
			_logger.LogWarning(
				"Stale PropertyUpdated signal dropped: stepIndex={StepIndex} out of recipe range (StepCount={StepCount})",
				stepIndex,
				recipe.StepCount);
			return;
		}

		if (stepIndex >= Items.Count)
		{
			_logger.LogWarning(
				"Stale PropertyUpdated signal dropped: stepIndex={StepIndex} exceeds Items.Count={ItemCount}",
				stepIndex,
				Items.Count);
			return;
		}

		RecipeRowUpdateSynchronizer.ApplyPropertyUpdate(RowOf(Items[stepIndex]), recipe.Steps[stepIndex]);
	}

	private void AppendItem(Recipe recipe, int index)
	{
		if (index < 0 || index >= recipe.StepCount)
		{
			_logger.LogWarning(
				"Stale StepAppended signal dropped: index={Index} out of recipe range (StepCount={StepCount})",
				index,
				recipe.StepCount);
			return;
		}

		Items.Add(CreateItemChecked(recipe.Steps[index], index + 1));
	}

	private void InsertItems(Recipe recipe, int startIndex, int count)
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

		if (startIndex > Items.Count)
		{
			_logger.LogWarning(
				"Stale StepsInserted signal dropped: startIndex={StartIndex} exceeds Items.Count={ItemCount}",
				startIndex,
				Items.Count);
			return;
		}

		for (var i = 0; i < count; i++)
		{
			var index = startIndex + i;
			Items.Insert(index, CreateItemChecked(recipe.Steps[index], index + 1));
		}

		RenumberItems(startIndex + count);
	}

	private void RemoveItem(int removedIndex)
	{
		if (removedIndex < 0 || removedIndex >= Items.Count)
		{
			_logger.LogWarning(
				"Stale StepRemoved signal dropped: removedIndex={RemovedIndex} out of Items range (Count={ItemCount})",
				removedIndex,
				Items.Count);
			return;
		}

		Items[removedIndex].Dispose();
		Items.RemoveAt(removedIndex);
		RenumberItems(removedIndex);
	}

	private void RemoveItems(IReadOnlyList<int> removedIndices)
	{
		if (removedIndices.Count == 0)
		{
			return;
		}

		foreach (var index in removedIndices)
		{
			if (index < 0 || index >= Items.Count)
			{
				_logger.LogWarning(
					"Stale StepsRemoved signal dropped: index={Index} out of Items range (Count={ItemCount})",
					index,
					Items.Count);
				return;
			}
		}

		foreach (var index in removedIndices.OrderByDescending(i => i))
		{
			Items[index].Dispose();
			Items.RemoveAt(index);
		}

		var minIndex = removedIndices.Min();
		RenumberItems(minIndex);
	}

	private void RebuildItem(Recipe recipe, int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= recipe.StepCount || stepIndex >= Items.Count)
		{
			_logger.LogWarning(
				"Stale StepActionChanged signal dropped: stepIndex={StepIndex}, recipe StepCount={StepCount}, Items.Count={ItemCount}",
				stepIndex,
				recipe.StepCount,
				Items.Count);
			return;
		}

		var step = recipe.Steps[stepIndex];
		var item = CreateItemChecked(step, stepIndex + 1);
		Items[stepIndex].Dispose();
		Items[stepIndex] = item;

		RowOf(item).MarkChanged(step.Properties.Keys.Select(id => id.Value));
	}

	private void RenumberItems(int fromIndex)
	{
		for (var i = fromIndex; i < Items.Count; i++)
		{
			RowOf(Items[i]).UpdateStepNumber(i + 1);
		}
	}

	private void FullRebuild(Recipe recipe)
	{
		_executionHighlightTracker.Reset();

		DisposeAllItems();
		Items.Clear();

		for (var i = 0; i < recipe.StepCount; i++)
		{
			Items.Add(CreateItemChecked(recipe.Steps[i], i + 1));
		}
	}

	private void RefreshStepStartTimes(int fromIndex)
	{
		var stepStartTimes = _coordinator.Snapshot.StepStartTimes;
		for (var i = fromIndex; i < Items.Count; i++)
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

			RowOf(Items[i]).UpdateStepStartTime(formattedTime);
		}
	}

	private void RefreshLoopDepths()
	{
		var rowLoopDepths = _coordinator.Snapshot.RowLoopDepths;
		for (var i = 0; i < Items.Count; i++)
		{
			RowOf(Items[i]).ForDepth = Math.Min(rowLoopDepths[i], 3);
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

	// Throws instead of skipping: a skipped step would leave Items.Count < recipe.StepCount and
	// silently desync every index-based dispatch. Config loading and import validation make the
	// branch unreachable; if the invariant is ever breached, Initialize() crashes, while
	// OnMutation catches this specific exception, error-logs it, reports it to the message
	// panel, and leaves the projection as-is.
	private TItem CreateItemChecked(Step step, int stepNumber)
	{
		var actionResult = RecipeMetadataRegistry.GetAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			throw new UnknownActionKeyException(
				$"Step {stepNumber}: unknown action key '{step.ActionKey}'");
		}

		var item = CreateItem(stepNumber, step, actionResult.Value);

		var row = RowOf(item);
		row.PropertyValueChanged += (columnKey, value) => OnCellValueChanged(item, columnKey, value);
		row.ActionChanged += actionId => OnActionChanged(item, actionId);
		row.SelectorValueChanged += selectorEdit => OnSelectorValueChanged(item, selectorEdit);

		return item;
	}

	private void DisposeAllItems()
	{
		foreach (var item in Items)
		{
			item.Dispose();
		}
	}
}
