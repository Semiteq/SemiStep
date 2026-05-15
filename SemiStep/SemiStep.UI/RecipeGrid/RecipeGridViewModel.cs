using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Threading;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeGrid;

public class RecipeGridViewModel : ReactiveObject, IDisposable
{
	private readonly ObservableAsPropertyHelper<bool> _canDeleteStep;
	private readonly ObservableAsPropertyHelper<bool> _isReadOnly;
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly ILogger<RecipeGridViewModel> _logger;
	private readonly MessagePanelViewModel _messagePanel;

	private IReadOnlyList<int> _selectedRowIndices = [];
	private bool _lastRecipeActive;
	private int _lastActualLine = -1;

	public RecipeGridViewModel(
		RecipeCoordinator coordinator,
		RecipeMetadataRegistry recipeMetadataRegistry,
		MessagePanelViewModel messagePanel,
		ILogger<RecipeGridViewModel> logger)
	{
		_coordinator = coordinator;
		RecipeMetadataRegistry = recipeMetadataRegistry;
		_messagePanel = messagePanel;
		_logger = logger;

		RecipeRows = new ObservableCollection<RecipeRowViewModel>();

		_canDeleteStep = this
			.WhenAnyValue(x => x.SelectedRowIndices)
			.Select(indices => indices.Count > 0)
			.ToProperty(this, x => x.CanDeleteStep)
			.DisposeWith(_disposables);

		_isReadOnly = coordinator.ExecutionState
			.Select(info => info.RecipeActive)
			.ToProperty(this, x => x.IsReadOnly)
			.DisposeWith(_disposables);

		coordinator.ExecutionState
			.Subscribe(OnExecutionStateChanged)
			.DisposeWith(_disposables);
	}

	internal RecipeMetadataRegistry RecipeMetadataRegistry { get; }

	public ObservableCollection<RecipeRowViewModel> RecipeRows { get; }

	public bool CanDeleteStep => _canDeleteStep.Value;

	public bool IsReadOnly => _isReadOnly.Value;

	public int SelectedRowIndex => _selectedRowIndices.Count > 0 ? _selectedRowIndices[0] : -1;

	public IReadOnlyList<int> SelectedRowIndices
	{
		get => _selectedRowIndices;
		set
		{
			this.RaiseAndSetIfChanged(ref _selectedRowIndices, value);
			this.RaisePropertyChanged(nameof(SelectedRowIndex));
		}
	}

	public event Action<int?>? SelectionRequested;

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

	private void OnExecutionStateChanged(PlcExecutionInfo info)
	{
		var activeChanged = info.RecipeActive != _lastRecipeActive;
		var lineChanged = info.ActualLine != _lastActualLine;

		if (!activeChanged && !lineChanged)
		{
			return;
		}

		if (!info.RecipeActive)
		{
			if (_lastRecipeActive)
			{
				ClearAllStepHighlights();
			}

			_lastRecipeActive = false;
			_lastActualLine = -1;

			return;
		}

		var previousLine = _lastActualLine;
		_lastRecipeActive = true;
		_lastActualLine = info.ActualLine;

		if (previousLine >= 0 && previousLine < RecipeRows.Count && previousLine != info.ActualLine)
		{
			RecipeRows[previousLine].IsCurrentStep = false;
			RecipeRows[previousLine].IsPastStep = previousLine < info.ActualLine;
		}

		if (info.ActualLine >= 0 && info.ActualLine < RecipeRows.Count)
		{
			RecipeRows[info.ActualLine].IsCurrentStep = true;
			RecipeRows[info.ActualLine].IsPastStep = false;
		}
	}

	private void ClearAllStepHighlights()
	{
		foreach (var row in RecipeRows)
		{
			row.IsCurrentStep = false;
			row.IsPastStep = false;
		}
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

		RefreshStepStartTimes();
	}

	public void RequestSelection(int? suggestedIndex)
	{
		SelectionRequested?.Invoke(suggestedIndex);
	}

	private void OnCellValueChanged(RecipeRowViewModel row, string columnKey, string? value)
	{
		if (value is null)
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
			_messagePanel.AddError($"Step {stepIndex + 1}: {result.Errors[0].Message}", "RecipeGrid");
		}
	}

	private void OnActionChanged(RecipeRowViewModel row, int newActionId)
	{
		var stepIndex = RecipeRows.IndexOf(row);
		if (stepIndex < 0)
		{
			return;
		}

		var result = _coordinator.ChangeStepAction(stepIndex, newActionId);

		if (result.IsFailed)
		{
			_messagePanel.AddError(
				$"Step {stepIndex + 1}: Failed to change action - {result.Errors[0].Message}", "RecipeGrid");
			return;
		}

		RequestSelection(result.Value);
	}

	private void UpdateAllRowsInPlace(Recipe recipe)
	{
		Debug.Assert(RecipeRows.Count == recipe.StepCount,
			"Row count mismatch: grid rows and recipe steps are out of sync.");

		for (var i = 0; i < recipe.StepCount; i++)
		{
			RecipeRows[i].UpdateStep(recipe.Steps[i]);
		}
	}

	private void LogStaleSignal(string signalKind, string contextTemplate, params object?[] contextArgs)
	{
		var args = new object?[contextArgs.Length + 1];
		args[0] = signalKind;
		Array.Copy(contextArgs, 0, args, 1, contextArgs.Length);
		_logger.LogWarning("Stale {SignalKind} signal dropped: " + contextTemplate, args);
	}

	private void UpdateSingleRowInPlace(Recipe recipe, int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= recipe.StepCount)
		{
			LogStaleSignal("PropertyUpdated", "stepIndex={StepIndex} out of recipe range (StepCount={StepCount})", stepIndex, recipe.StepCount);
			return;
		}

		if (stepIndex >= RecipeRows.Count)
		{
			UpdateAllRowsInPlace(recipe);

			return;
		}

		RecipeRows[stepIndex].UpdateStep(recipe.Steps[stepIndex]);
	}

	private void AppendRow(Recipe recipe, int index)
	{
		if (index < 0 || index >= recipe.StepCount)
		{
			LogStaleSignal("StepAppended", "index={Index} out of recipe range (StepCount={StepCount})", index, recipe.StepCount);
			return;
		}

		var step = recipe.Steps[index];
		var action = RecipeMetadataRegistry.GetAction(step.ActionKey).Value;
		RecipeRows.Add(CreateRowViewModel(step, action, index + 1));
	}

	private void InsertRows(Recipe recipe, int startIndex, int count)
	{
		if (startIndex < 0 || count <= 0 || startIndex + count > recipe.StepCount)
		{
			LogStaleSignal("StepsInserted", "startIndex={StartIndex}, count={Count}, recipe StepCount={StepCount}", startIndex, count, recipe.StepCount);
			return;
		}

		if (startIndex > RecipeRows.Count)
		{
			LogStaleSignal("StepsInserted", "startIndex={StartIndex} exceeds RecipeRows.Count={RowCount}", startIndex, RecipeRows.Count);
			return;
		}

		for (var i = 0; i < count; i++)
		{
			var index = startIndex + i;
			var step = recipe.Steps[index];
			var action = RecipeMetadataRegistry.GetAction(step.ActionKey).Value;
			RecipeRows.Insert(index, CreateRowViewModel(step, action, index + 1));
		}

		RenumberRows(startIndex + count);
	}

	private void RemoveRow(int removedIndex)
	{
		if (removedIndex < 0 || removedIndex >= RecipeRows.Count)
		{
			LogStaleSignal("StepRemoved", "removedIndex={RemovedIndex} out of RecipeRows range (Count={RowCount})", removedIndex, RecipeRows.Count);
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
				LogStaleSignal("StepsRemoved", "index={Index} out of RecipeRows range (Count={RowCount})", index, RecipeRows.Count);
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
			LogStaleSignal("StepActionChanged", "stepIndex={StepIndex}, recipe StepCount={StepCount}, RecipeRows.Count={RowCount}", stepIndex, recipe.StepCount, RecipeRows.Count);
			return;
		}

		RecipeRows[stepIndex].Dispose();
		var step = recipe.Steps[stepIndex];
		var action = RecipeMetadataRegistry.GetAction(step.ActionKey).Value;
		RecipeRows[stepIndex] = CreateRowViewModel(step, action, stepIndex + 1);
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
		_lastRecipeActive = false;
		_lastActualLine = -1;

		DisposeAllRows();
		RecipeRows.Clear();

		for (var i = 0; i < recipe.StepCount; i++)
		{
			var step = recipe.Steps[i];
			var action = RecipeMetadataRegistry.GetAction(step.ActionKey).Value;
			RecipeRows.Add(CreateRowViewModel(step, action, i + 1));
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

	public List<Step> CollectSelectedSteps()
	{
		var recipe = _coordinator.CurrentRecipe;

		return _selectedRowIndices
			.OrderBy(i => i)
			.Select(i => recipe.Steps[i])
			.ToList();
	}

	private RecipeRowViewModel CreateRowViewModel(
		Step step,
		ActionDefinition action,
		int stepNumber)
	{
		var cellStates = new Dictionary<string, CellState>(StringComparer.OrdinalIgnoreCase);
		foreach (var col in RecipeMetadataRegistry.GetAllColumns())
		{
			cellStates[col.Key] = CellStateResolver.GetCellState(col, action);
		}

		var row = new RecipeRowViewModel(
			stepNumber,
			step,
			action,
			RecipeMetadataRegistry,
			cellStates);

		row.PropertyValueChanged += (columnKey, value) => OnCellValueChanged(row, columnKey, value);
		row.ActionChanged += actionId => OnActionChanged(row, actionId);

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
