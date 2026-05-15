using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public class RecipeRowViewModel(
	int stepNumber,
	Step step,
	ActionDefinition action,
	RecipeMetadataRegistry recipeMetadataRegistry,
	IReadOnlyDictionary<string, CellState> cellStates)
	: ReactiveObject, IDisposable
{
	private Step _step = step;

	private readonly (IReadOnlyDictionary<string, string?> Units, IReadOnlyDictionary<string, string> FormatKinds) _columnMetadata
		= BuildColumnMetadata(action, recipeMetadataRegistry);

	private readonly IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> _groupItemsByColumn
		= BuildGroupItemsByColumn(action, recipeMetadataRegistry);

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

	public IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> GroupItemsByColumn => _groupItemsByColumn;

	public object? this[string columnKey]
	{
		get => GetPropertyValue(columnKey);
		set => SetPropertyValue(columnKey, value?.ToString());
	}

	public event Action<string, string?>? PropertyValueChanged;
	public event Action<int>? ActionChanged;

	public void Dispose()
	{
		PropertyValueChanged = null;
		ActionChanged = null;
		GC.SuppressFinalize(this);
	}

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

	private static PropertyTypeDefinition? ResolvePropertyType(
		ActionPropertyDefinition actionProperty,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var propertyResult = recipeMetadataRegistry.GetProperty(actionProperty.PropertyTypeId);
		return propertyResult.IsSuccess ? propertyResult.Value : null;
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> BuildGroupItemsByColumn(
		ActionDefinition actionDefinition,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var groupItemsByColumn = new Dictionary<string, IReadOnlyList<ComboBoxItemViewModel>>(StringComparer.OrdinalIgnoreCase);

		foreach (var columnDefinition in recipeMetadataRegistry.GetAllColumns())
		{
			if (!ColumnTypes.IsGroupBoundColumn(columnDefinition.ColumnType))
			{
				continue;
			}

			groupItemsByColumn[columnDefinition.Key] = Array.Empty<ComboBoxItemViewModel>();
		}

		foreach (var actionProperty in actionDefinition.Properties)
		{
			if (actionProperty.GroupName is null)
			{
				continue;
			}

			var groupResult = recipeMetadataRegistry.GetGroup(actionProperty.GroupName);
			if (groupResult.IsFailed)
			{
				continue;
			}

			groupItemsByColumn[actionProperty.Key] = groupResult.Value.Items
				.Select(kvp => new ComboBoxItemViewModel(kvp.Key, kvp.Value))
				.OrderBy(item => item.Id)
				.ToList();
		}

		return groupItemsByColumn;
	}

	private static (IReadOnlyDictionary<string, string?> Units, IReadOnlyDictionary<string, string> FormatKinds) BuildColumnMetadata(
		ActionDefinition actionDefinition,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var units = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
		var formatKinds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var actionProperty in actionDefinition.Properties)
		{
			var propertyType = ResolvePropertyType(actionProperty, recipeMetadataRegistry);
			units[actionProperty.Key] = propertyType?.Units;
			formatKinds[actionProperty.Key] = propertyType?.FormatKind ?? TimeFormatHelper.DefaultFormatKind;
		}

		units[TimeFormatHelper.StepStartTimeColumnKey] = TimeFormatHelper.TimeUnits;
		formatKinds[TimeFormatHelper.StepStartTimeColumnKey] = TimeFormatHelper.TimeHmsFormat;

		return (units, formatKinds);
	}
}
