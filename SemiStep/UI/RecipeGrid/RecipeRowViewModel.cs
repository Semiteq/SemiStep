using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace UI.RecipeGrid;

public class RecipeRowViewModel(
	int stepNumber,
	Step step,
	ActionDefinition action,
	ConfigRegistry configRegistry,
	IReadOnlyDictionary<string, CellState> cellStates)
	: ReactiveObject, IDisposable
{
	private Step _step = step;

	private readonly (IReadOnlyDictionary<string, string?> Units, IReadOnlyDictionary<string, string> FormatKinds) _columnMetadata
		= BuildColumnMetadata(action, configRegistry);

	public int StepNumber
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = stepNumber;

	public string? StepStartTime
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public int ActionId => _step.ActionKey;

	public string ActionName => action.UiName;

	public bool IsCurrentStep
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public bool IsPastStep
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public IReadOnlyDictionary<string, CellState> CellStates { get; } = cellStates;

	public IReadOnlyDictionary<string, string?> ColumnUnits => _columnMetadata.Units;

	public IReadOnlyDictionary<string, string> ColumnFormatKinds => _columnMetadata.FormatKinds;

	public object? this[string columnKey]
	{
		get => GetPropertyValue(columnKey);
		set => SetPropertyValue(columnKey, value?.ToString());
	}

	public void Dispose()
	{
		PropertyValueChanged = null;
		ActionChanged = null;
		GC.SuppressFinalize(this);
	}

	public event Action<string, string?>? PropertyValueChanged;
	public event Action<int>? ActionChanged;

	public void UpdateStep(Step newStep)
	{
		_step = newStep;
		this.RaisePropertyChanged("Item[]");
	}

	public void UpdateStepNumber(int newNumber)
	{
		StepNumber = newNumber;
	}

	public void UpdateStepStartTime(string? formattedTime)
	{
		StepStartTime = formattedTime;
	}

	public object? GetPropertyValue(string columnKey)
	{
		if (columnKey == ColumnTypes.Action)
		{
			return ActionId;
		}

		if (columnKey == TimeFormatHelper.StepStartTimeColumnKey)
		{
			return StepStartTime;
		}

		var columnId = new PropertyId(columnKey);
		if (_step.Properties.TryGetValue(columnId, out var propertyValue))
		{
			return propertyValue.Value;
		}

		return null;
	}

	public void SetPropertyValue(string columnKey, string? value)
	{
		if (columnKey == ColumnTypes.Action)
		{
			if (int.TryParse(value, out var actionId))
			{
				ActionChanged?.Invoke(actionId);
			}

			return;
		}

		PropertyValueChanged?.Invoke(columnKey, value);
	}

	public string? GetGroupNameForColumn(string columnKey)
	{
		var actionColumn = action.Properties.FirstOrDefault(c => c.Key == columnKey);

		return actionColumn?.GroupName;
	}

	public IReadOnlyDictionary<int, string>? GetGroupItemsForColumn(string columnKey)
	{
		var groupName = GetGroupNameForColumn(columnKey);
		if (groupName is null)
		{
			return null;
		}

		var groupResult = configRegistry.GetGroup(groupName);
		if (groupResult.IsFailed)
		{
			return null;
		}

		return groupResult.Value.Items;
	}

	private static PropertyTypeDefinition? ResolvePropertyType(
		ActionPropertyDefinition actionProperty,
		ConfigRegistry configRegistry)
	{
		var propertyResult = configRegistry.GetProperty(actionProperty.PropertyTypeId);
		return propertyResult.IsSuccess ? propertyResult.Value : null;
	}

	private static (IReadOnlyDictionary<string, string?> Units, IReadOnlyDictionary<string, string> FormatKinds) BuildColumnMetadata(
		ActionDefinition actionDefinition,
		ConfigRegistry configRegistry)
	{
		var units = new Dictionary<string, string?>(StringComparer.Ordinal);
		var formatKinds = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var actionProperty in actionDefinition.Properties)
		{
			var propertyType = ResolvePropertyType(actionProperty, configRegistry);
			units[actionProperty.Key] = propertyType?.Units;
			formatKinds[actionProperty.Key] = propertyType?.FormatKind ?? TimeFormatHelper.DefaultFormatKind;
		}

		units[TimeFormatHelper.StepStartTimeColumnKey] = TimeFormatHelper.TimeUnits;
		formatKinds[TimeFormatHelper.StepStartTimeColumnKey] = TimeFormatHelper.TimeHmsFormat;

		return (units, formatKinds);
	}
}
