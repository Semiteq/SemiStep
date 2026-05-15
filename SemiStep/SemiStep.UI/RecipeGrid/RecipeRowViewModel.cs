using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public sealed class RecipeRowViewModel(
	int stepNumber,
	Step step,
	ActionDefinition action,
	RecipeMetadataRegistry recipeMetadataRegistry,
	IReadOnlySet<string> inapplicableColumns)
	: ReactiveObject, IDisposable
{
	private const string IndexerName = "Item[]";

	private readonly (IReadOnlyDictionary<string, string?> Units, IReadOnlyDictionary<string, string> FormatKinds) _columnMetadata
		= BuildColumnMetadata(action, recipeMetadataRegistry);

	private readonly IReadOnlyDictionary<string, string> _groupNamesByColumn
		= BuildGroupNamesByColumn(action);

	private Step _step = step;

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

	public IReadOnlySet<string> InapplicableColumns { get; } = inapplicableColumns;

	public IReadOnlyDictionary<string, string?> ColumnUnits => _columnMetadata.Units;

	public IReadOnlyDictionary<string, string> ColumnFormatKinds => _columnMetadata.FormatKinds;

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
	}

	public void UpdateStep(Step newStep)
	{
		_step = newStep;
		this.RaisePropertyChanged(IndexerName);
	}

	public void UpdateStepNumber(int newNumber)
	{
		StepNumber = newNumber;
	}

	public void UpdateStepStartTime(string? formattedTime)
	{
		StepStartTime = formattedTime;
	}

	public bool IsApplicable(string columnKey)
	{
		return !InapplicableColumns.Contains(columnKey);
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
			if (!int.TryParse(value, out var actionId))
			{
				return;
			}

			if (actionId == _step.ActionKey)
			{
				return;
			}

			ActionChanged?.Invoke(actionId);
			return;
		}

		var currentValue = GetPropertyValue(columnKey)?.ToString();
		if (string.Equals(currentValue, value, StringComparison.Ordinal))
		{
			return;
		}

		PropertyValueChanged?.Invoke(columnKey, value);
	}

	public string? GetGroupNameForColumn(string columnKey)
	{
		return _groupNamesByColumn.TryGetValue(columnKey, out var groupName) ? groupName : null;
	}

	private static PropertyTypeDefinition? ResolvePropertyType(
		ActionPropertyDefinition actionProperty,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var propertyResult = recipeMetadataRegistry.GetProperty(actionProperty.PropertyTypeId);
		return propertyResult.IsSuccess ? propertyResult.Value : null;
	}

	private static IReadOnlyDictionary<string, string> BuildGroupNamesByColumn(ActionDefinition actionDefinition)
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var actionProperty in actionDefinition.Properties)
		{
			if (actionProperty.GroupName is null)
			{
				continue;
			}

			result[actionProperty.Key] = actionProperty.GroupName;
		}

		return result;
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
