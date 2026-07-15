using System.Globalization;
using System.Runtime.CompilerServices;

using ReactiveUI;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

namespace SemiStep.UI.RecipeGrid;

public sealed class RecipeRowViewModel(
	int stepNumber,
	Step step,
	ActionDefinition action,
	RecipeMetadataRegistry recipeMetadataRegistry,
	IReadOnlySet<string> inapplicableColumns)
	: ReactiveObject, IDisposable
{
	private const string IndexerName = "Item";

	// Cached per action (stable ActionDefinition instance makes the reference key safe); weak table drops entries when an action is collected.
	private static readonly ConditionalWeakTable<ActionDefinition, ActionColumnMetadata> _actionMetadataCache = new();

	private readonly ActionColumnMetadata _actionMetadata = GetActionMetadata(action, recipeMetadataRegistry);

	private Step _step = step;

	internal Step CurrentStep => _step;

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

	public int ForDepth
	{
		get;
		set
		{
			if (field == value)
			{
				return;
			}

			this.RaiseAndSetIfChanged(ref field, value);
			this.RaisePropertyChanged(nameof(IsForDepth1));
			this.RaisePropertyChanged(nameof(IsForDepth2));
			this.RaisePropertyChanged(nameof(IsForDepth3));
		}
	}

	public bool IsForDepth1 => ForDepth == 1;

	public bool IsForDepth2 => ForDepth == 2;

	// `>= 3` is defense-in-depth: ForDepth is capped at 3 upstream in RefreshLoopDepths,
	// but using `>=` guards against future UI cap drift so deeper nestings still render.
	public bool IsForDepth3 => ForDepth >= 3;

	public IReadOnlySet<string> InapplicableColumns
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = inapplicableColumns;

	public IReadOnlySet<string> ChangedColumns
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public IReadOnlyDictionary<string, string?> ColumnUnits => _actionMetadata.Units;

	public IReadOnlyDictionary<string, string> ColumnFormatKinds => _actionMetadata.FormatKinds;

	public IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> GroupItemsByColumn =>
		_actionMetadata.GroupItemsByColumn;

	public object? this[string columnKey]
	{
		get => GetPropertyValue(columnKey);
		set => SetPropertyValue(columnKey, value?.ToString());
	}

	public event Action<string, string?>? PropertyValueChanged;
	public event Action<int>? ActionChanged;
	public event Action<SelectorEdit>? SelectorValueChanged;

	public void Dispose()
	{
		PropertyValueChanged = null;
		ActionChanged = null;
		SelectorValueChanged = null;
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
			if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actionId))
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

		if (TryBuildSelectorEdit(columnKey, value, out var selectorEdit))
		{
			SelectorValueChanged?.Invoke(selectorEdit);
			return;
		}

		PropertyValueChanged?.Invoke(columnKey, value);
	}

	/// <summary>
	/// Recomputes <see cref="InapplicableColumns"/> from the row's current step. When the result
	/// differs it assigns a NEW set instance — the OneWay cell binding only re-fires on a
	/// PropertyChanged that also carries a reference change, so the value is replaced rather than
	/// mutated in place. An unchanged result keeps the current instance and raises nothing, so
	/// callers may recompute freely on every property update.
	/// </summary>
	public void RecomputeInapplicableColumns()
	{
		var recomputed = BuildInapplicableColumns(action, _step, recipeMetadataRegistry);
		if (InapplicableColumns.SetEquals(recomputed))
		{
			return;
		}

		InapplicableColumns = recomputed;
	}

	/// <summary>
	/// Replaces <see cref="ChangedColumns"/> with a fresh set built from <paramref name="keys"/>.
	/// Like <see cref="InapplicableColumns"/>, the OneWay cell binding only re-fires on a reference
	/// change, so every mutator assigns a NEW set instance rather than mutating in place.
	/// </summary>
	public void MarkChanged(IEnumerable<string> keys)
	{
		ChangedColumns = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
	}

	public void ApplyChangedDelta(IReadOnlyCollection<string> add, IReadOnlyCollection<string> remove)
	{
		var next = new HashSet<string>(ChangedColumns, StringComparer.OrdinalIgnoreCase);
		next.UnionWith(add);
		next.ExceptWith(remove);
		ChangedColumns = next;
	}

	public void ClearChanged(string columnKey)
	{
		if (!ChangedColumns.Contains(columnKey))
		{
			return;
		}

		var next = new HashSet<string>(ChangedColumns, StringComparer.OrdinalIgnoreCase);
		next.Remove(columnKey);
		ChangedColumns = next;
	}

	public void ClearAllChanged()
	{
		if (ChangedColumns.Count == 0)
		{
			return;
		}

		ChangedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	}

	public bool IsChanged(string columnKey)
	{
		return ChangedColumns.Contains(columnKey);
	}

	/// <summary>
	/// A column is a selector when a union column carries an ActivationCondition keyed on it.
	/// </summary>
	private bool TryBuildSelectorEdit(string columnKey, string? value, out SelectorEdit selectorEdit)
	{
		selectorEdit = default!;

		if (!IsSelectorColumn(columnKey))
		{
			return false;
		}

		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var selectorId))
		{
			return false;
		}

		var oldActive = ActiveColumnSetResolver.Resolve(action, _step);
		var candidateStep = _step.WithProperty(columnKey, PropertyValue.FromInt(selectorId));
		var newActive = ActiveColumnSetResolver.Resolve(action, candidateStep);

		var columnsToDrop = new List<string>();
		var columnsToSeed = new List<string>();

		foreach (var property in action.Properties)
		{
			var key = property.Key;
			var wasActive = oldActive.Contains(key);
			var nowActive = newActive.Contains(key);

			if (wasActive && !nowActive)
			{
				columnsToDrop.Add(key);
			}
			else if (!wasActive && nowActive)
			{
				columnsToSeed.Add(key);
			}
		}

		selectorEdit = new SelectorEdit(
			columnKey,
			selectorId.ToString(CultureInfo.InvariantCulture),
			columnsToDrop,
			columnsToSeed);
		return true;
	}

	private bool IsSelectorColumn(string columnKey)
	{
		foreach (var property in action.Properties)
		{
			if (property.Activation is null)
			{
				continue;
			}

			foreach (var condition in property.Activation)
			{
				if (string.Equals(condition.SelectorKey, columnKey, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}

		return false;
	}

	public static IReadOnlySet<string> BuildInapplicableColumns(
		ActionDefinition action,
		Step step,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var activeColumnKeys = ActiveColumnSetResolver.Resolve(action, step);
		var inapplicable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in recipeMetadataRegistry.GetAllColumns())
		{
			if (CellStateResolver.IsInapplicable(column, activeColumnKeys))
			{
				inapplicable.Add(column.Key);
			}
		}

		return inapplicable;
	}

	private static PropertyTypeDefinition? ResolvePropertyType(
		ActionPropertyDefinition actionProperty,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var propertyResult = recipeMetadataRegistry.GetProperty(actionProperty.PropertyTypeId);
		return propertyResult.IsSuccess ? propertyResult.Value : null;
	}

	private static ActionColumnMetadata GetActionMetadata(
		ActionDefinition action,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		return _actionMetadataCache.GetValue(
			action,
			resolvedAction =>
			{
				var (units, formatKinds) = BuildColumnMetadata(resolvedAction, recipeMetadataRegistry);
				return new ActionColumnMetadata(
					units,
					formatKinds,
					BuildGroupItemsByColumn(resolvedAction, recipeMetadataRegistry));
			});
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> BuildGroupItemsByColumn(
		ActionDefinition actionDefinition,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		// Pre-populate group-bound columns with empty lists so a recycle onto an action lacking the property doesn't throw KeyNotFoundException.
		var result = new Dictionary<string, IReadOnlyList<ComboBoxItemViewModel>>(StringComparer.OrdinalIgnoreCase);

		foreach (var column in recipeMetadataRegistry.GetAllColumns())
		{
			if (ColumnTypes.IsGroupBoundColumn(column.ColumnType))
			{
				result[column.Key] = Array.Empty<ComboBoxItemViewModel>();
			}
		}

		foreach (var actionProperty in actionDefinition.Properties)
		{
			if (actionProperty.GroupName is null)
			{
				continue;
			}

			// Validation already rejects unknown keys; this guard confines the overlay to pre-populated columns.
			if (!result.ContainsKey(actionProperty.Key))
			{
				continue;
			}

			result[actionProperty.Key] = recipeMetadataRegistry.GetComboBoxItems(actionProperty.GroupName);
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

	private sealed record ActionColumnMetadata(
		IReadOnlyDictionary<string, string?> Units,
		IReadOnlyDictionary<string, string> FormatKinds,
		IReadOnlyDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> GroupItemsByColumn);
}
