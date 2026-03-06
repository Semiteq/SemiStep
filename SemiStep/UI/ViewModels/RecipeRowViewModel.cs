using Core.Entities;

using Domain.Services;

using ReactiveUI;

using Shared.Entities;
using Shared.Registries;

namespace UI.ViewModels;

public class RecipeRowViewModel(
	int stepNumber,
	Step step,
	ActionDefinition action,
	IGroupRegistry groupRegistry,
	IColumnRegistry columnRegistry,
	IPropertyRegistry propertyRegistry,
	Action<RecipeRowViewModel, string, string?> onPropertyChanged,
	Action<RecipeRowViewModel, int> onActionChanged)
	: ReactiveObject, IDisposable
{
	private readonly List<Action> _cleanupActions = [];

	private ActionDefinition _action = action;
	private IReadOnlyDictionary<string, CellState>? _cellStatesCache;
	private bool _disposed;
	private bool _isExecuting;
	private int _stepNumber = stepNumber;
	private string? _stepStartTime;
	private Step _step = step;

	public int StepNumber
	{
		get => _stepNumber;
		private set => this.RaiseAndSetIfChanged(ref _stepNumber, value);
	}

	public string? StepStartTime
	{
		get => _stepStartTime;
		private set => this.RaiseAndSetIfChanged(ref _stepStartTime, value);
	}

	public int ActionId => _step.ActionKey;

	public string ActionName => _action.UiName;

	public bool IsExecuting
	{
		get => _isExecuting;
		set => this.RaiseAndSetIfChanged(ref _isExecuting, value);
	}

	public IReadOnlyDictionary<string, CellState> CellStates
	{
		get
		{
			if (_cellStatesCache is not null)
			{
				return _cellStatesCache;
			}

			var states = new Dictionary<string, CellState>();
			foreach (var columnDef in columnRegistry.GetAll())
			{
				states[columnDef.Key] = CellStateResolver.GetCellState(columnDef, _action);
			}

			_cellStatesCache = states;

			return _cellStatesCache;
		}
	}

	public object? this[string columnKey]
	{
		get => GetPropertyValue(columnKey);
		set => SetPropertyValue(columnKey, value?.ToString());
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var cleanup in _cleanupActions)
		{
			try
			{
				cleanup();
			}
			catch
			{
				// Ignore cleanup errors
			}
		}

		_cleanupActions.Clear();
	}

	public void UpdateStep(Step newStep)
	{
		_step = newStep;
	}

	public void UpdateStep(Step newStep, ActionDefinition newAction)
	{
		_step = newStep;
		_action = newAction;
		_cellStatesCache = null;
		this.RaisePropertyChanged(nameof(ActionId));
		this.RaisePropertyChanged(nameof(ActionName));
		this.RaisePropertyChanged(nameof(CellStates));
	}

	public void UpdateStepNumber(int newNumber)
	{
		StepNumber = newNumber;
	}

	public void UpdateStepStartTime(string? formattedTime)
	{
		StepStartTime = formattedTime;
		this.RaisePropertyChanged("Item[]");
	}

	public object? GetPropertyValue(string columnKey)
	{
		if (columnKey is "action")
		{
			return ActionId;
		}

		if (columnKey is "step_start_time")
		{
			return StepStartTime;
		}

		var columnId = new ColumnId(columnKey);
		if (_step.Properties.TryGetValue(columnId, out var propertyValue))
		{
			return propertyValue.Value;
		}

		return null;
	}

	public void SetPropertyValue(string columnKey, string? value)
	{
		if (columnKey == "action")
		{
			if (int.TryParse(value, out var actionId))
			{
				onActionChanged(this, actionId);
			}

			return;
		}

		onPropertyChanged(this, columnKey, value);
		this.RaisePropertyChanged(columnKey);
		this.RaisePropertyChanged("Item[]");
	}

	public IReadOnlyDictionary<int, string>? GetGroupItemsForColumn(string columnKey)
	{
		var actionColumn = _action.Columns.FirstOrDefault(c => c.Key == columnKey);
		if (actionColumn?.GroupName is null)
		{
			return null;
		}

		if (!groupRegistry.GroupExists(actionColumn.GroupName))
		{
			return null;
		}

		return groupRegistry.GetGroup(actionColumn.GroupName).Items;
	}

	public string? GetUnitsForColumn(string columnKey)
	{
		var actionColumn = _action.Columns.FirstOrDefault(c => c.Key == columnKey);
		if (actionColumn is null)
		{
			return null;
		}

		if (!propertyRegistry.PropertyExists(actionColumn.PropertyTypeId))
		{
			return null;
		}

		var propDef = propertyRegistry.GetProperty(actionColumn.PropertyTypeId);
		return propDef.Units;
	}

	public string GetFormatKindForColumn(string columnKey)
	{
		var actionColumn = _action.Columns.FirstOrDefault(c => c.Key == columnKey);
		if (actionColumn is null)
		{
			return "numeric";
		}

		if (!propertyRegistry.PropertyExists(actionColumn.PropertyTypeId))
		{
			return "numeric";
		}

		var propDef = propertyRegistry.GetProperty(actionColumn.PropertyTypeId);
		return propDef.FormatKind;
	}

	public void InvalidateCellStates()
	{
		_cellStatesCache = null;
		this.RaisePropertyChanged(nameof(CellStates));
	}

	/// <summary>
	/// Registers a cleanup action to be called when this row is disposed.
	/// Used to unsubscribe event handlers.
	/// </summary>
	public void RegisterCleanup(Action cleanupAction)
	{
		if (_disposed)
		{
			// Already disposed, execute immediately
			cleanupAction();

			return;
		}

		_cleanupActions.Add(cleanupAction);
	}
}
