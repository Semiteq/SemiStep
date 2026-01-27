using Domain.Services;

using ReactiveUI;

using Recipe.Entities;

using Shared.Entities;
using Shared.Registries;

namespace UI.ViewModels;

public class RecipeRowViewModel : ReactiveObject
{
	private readonly Step _step;
	private readonly ActionDefinition _action;
	private readonly IGroupRegistry _groupRegistry;
	private readonly IColumnRegistry _columnRegistry;
	private readonly CellStateResolver _cellStateResolver;
	private readonly Action<int, string, object?> _onPropertyChanged;
	private readonly Action<int, short> _onActionChanged;

	private bool _isExecuting;
	private bool _isSelected;
	private IReadOnlyDictionary<string, CellState>? _cellStatesCache;

	public RecipeRowViewModel(
		int stepNumber,
		Step step,
		ActionDefinition action,
		IGroupRegistry groupRegistry,
		IColumnRegistry columnRegistry,
		CellStateResolver cellStateResolver,
		Action<int, string, object?> onPropertyChanged,
		Action<int, short> onActionChanged)
	{
		StepNumber = stepNumber;
		_step = step;
		_action = action;
		_groupRegistry = groupRegistry;
		_columnRegistry = columnRegistry;
		_cellStateResolver = cellStateResolver;
		_onPropertyChanged = onPropertyChanged;
		_onActionChanged = onActionChanged;
	}

	public int StepNumber { get; }

	public short ActionId => short.Parse(_step.ActionKey);

	public string ActionName => _action.UiName;

	public bool IsExecuting
	{
		get => _isExecuting;
		set => this.RaiseAndSetIfChanged(ref _isExecuting, value);
	}

	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			if (this.RaiseAndSetIfChanged(ref _isSelected, value))
			{
				this.RaisePropertyChanged(nameof(CellStates));
			}
		}
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
			foreach (var columnDef in _columnRegistry.GetAll())
			{
				states[columnDef.Key] = _cellStateResolver.GetCellState(_step, columnDef, _action);
			}
			_cellStatesCache = states;
			return _cellStatesCache;
		}
	}

	public object? this[string columnKey]
	{
		get => GetPropertyValue(columnKey);
		set => SetPropertyValue(columnKey, value);
	}

	public object? GetPropertyValue(string columnKey)
	{
		if (columnKey == "action")
		{
			return ActionId;
		}

		var columnId = new ColumnId(columnKey);
		if (_step.Properties.TryGetValue(columnId, out var propertyValue))
		{
			return propertyValue.Value;
		}

		return null;
	}

	public void SetPropertyValue(string columnKey, object? value)
	{
		if (columnKey == "action")
		{
			if (value is short actionId)
			{
				_onActionChanged(StepNumber - 1, actionId);
			}
			return;
		}

		_onPropertyChanged(StepNumber - 1, columnKey, value);
		this.RaisePropertyChanged(columnKey);
		this.RaisePropertyChanged("Item[]");
		InvalidateCellStates();
	}

	public IReadOnlyDictionary<int, string>? GetGroupItemsForColumn(string columnKey)
	{
		var actionColumn = _action.Columns.FirstOrDefault(c => c.Key == columnKey);
		if (actionColumn is null)
		{
			return null;
		}

		if (HasGroupForColumn(columnKey) is false)
		{
			return null;
		}

		if (actionColumn.GroupName is null)
		{
			return null;
		}

		if (_groupRegistry.GroupExists(actionColumn.GroupName) is false)
		{
			return null;
		}

		return _groupRegistry.GetGroup(actionColumn.GroupName).Items;
	}

	public void InvalidateCellStates()
	{
		_cellStatesCache = null;
		this.RaisePropertyChanged(nameof(CellStates));
	}

	private bool HasGroupForColumn(string columnKey)
	{
		var actionColumn = _action.Columns.FirstOrDefault(c => c.Key == columnKey);
		return actionColumn?.GroupName is not null && _groupRegistry.GroupExists(actionColumn.GroupName);
	}
}
