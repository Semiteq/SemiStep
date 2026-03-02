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
	Action<int, string, object?> onPropertyChanged,
	Action<int, int> onActionChanged)
	: ReactiveObject, IDisposable
{
	private readonly List<Action> _cleanupActions = [];
	private IReadOnlyDictionary<string, CellState>? _cellStatesCache;
	private bool _disposed;
	private bool _isExecuting;
	private Step _step = step;

	public int StepNumber { get; } = stepNumber;

	public int ActionId => _step.ActionKey;

	public string ActionName => action.UiName;

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
				states[columnDef.Key] = CellStateResolver.GetCellState(columnDef, action);
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

	public object? GetPropertyValue(string columnKey)
	{
		if (columnKey is "action")
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
			if (value is int actionId)
			{
				onActionChanged(StepNumber - 1, actionId);
			}

			return;
		}

		onPropertyChanged(StepNumber - 1, columnKey, value);
		this.RaisePropertyChanged(columnKey);
		this.RaisePropertyChanged("Item[]");
	}

	public IReadOnlyDictionary<int, string>? GetGroupItemsForColumn(string columnKey)
	{
		var actionColumn = action.Columns.FirstOrDefault(c => c.Key == columnKey);
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

		if (groupRegistry.GroupExists(actionColumn.GroupName) is false)
		{
			return null;
		}

		return groupRegistry.GetGroup(actionColumn.GroupName).Items;
	}

	public void InvalidateCellStates()
	{
		_cellStatesCache = null;
		this.RaisePropertyChanged(nameof(CellStates));
	}

	private bool HasGroupForColumn(string columnKey)
	{
		var actionColumn = action.Columns.FirstOrDefault(c => c.Key == columnKey);

		return actionColumn?.GroupName is not null && groupRegistry.GroupExists(actionColumn.GroupName);
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
