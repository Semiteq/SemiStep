using System.Globalization;

using ReactiveUI;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

namespace SemiStep.UI.RecipeGrid;

public sealed class RecipeRowViewModel(
	int stepNumber,
	Step step,
	ActionDefinition action,
	RecipeMetadataRegistry recipeMetadataRegistry,
	ComboBoxItemsProvider comboBoxItemsProvider,
	IReadOnlySet<string> inapplicableColumns)
	: ReactiveObject, IDisposable
{
	private const string IndexerName = "Item";
	// U+25BA: the chrome fonts (Segoe UI, Segoe UI Variable) have no U+25B6 "▶" and would fall back for it.
	internal const string RunningGlyph = "►";
	internal const string StopGlyph = "■";

	private static readonly PropertyValue _emptyValue = PropertyValue.FromString(string.Empty);

	private readonly ActionColumnMetadata _actionMetadata =
		ActionColumnMetadata.For(action, recipeMetadataRegistry, comboBoxItemsProvider);

	internal Step CurrentStep { get; private set; } = step;

	public int StepNumber
	{
		get;
		private set
		{
			if (field == value)
			{
				return;
			}

			this.RaiseAndSetIfChanged(ref field, value);
			this.RaisePropertyChanged(nameof(StepNumberText));
		}
	} = stepNumber;

	/// <summary>The № cell text: the step number, after "►" on the running row and "■" on the row a run stopped on.</summary>
	public string StepNumberText => IsCurrentStep
		? string.Create(CultureInfo.InvariantCulture, $"{RunningGlyph} {StepNumber}")
		: IsStopStep
			? string.Create(CultureInfo.InvariantCulture, $"{StopGlyph} {StepNumber}")
			: StepNumber.ToString(CultureInfo.InvariantCulture);

	public string? StepStartTime
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public int ActionId => CurrentStep.ActionKey;

	public bool IsCurrentStep
	{
		get;
		set
		{
			if (field == value)
			{
				return;
			}

			this.RaiseAndSetIfChanged(ref field, value);
			this.RaisePropertyChanged(nameof(StepNumberText));
		}
	}

	public bool IsPastStep
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public bool IsStopStep
	{
		get;
		set
		{
			if (field == value)
			{
				return;
			}

			this.RaiseAndSetIfChanged(ref field, value);
			this.RaisePropertyChanged(nameof(StepNumberText));
		}
	}

	/// <summary>"(pass/count)" while the loop this row starts is running; null otherwise.</summary>
	public string? LoopPassText
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public int ForDepth
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	}

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
		CurrentStep = newStep;
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
		return CurrentStep.Properties.TryGetValue(columnId, out var propertyValue) ? propertyValue.Value : null;
	}

	public void SetPropertyValue(string columnKey, string? value)
	{
		if (columnKey == ColumnTypes.Action)
		{
			if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actionId))
			{
				return;
			}

			if (actionId == CurrentStep.ActionKey)
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
		var recomputed = BuildInapplicableColumns(action, CurrentStep, recipeMetadataRegistry);
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
	/// Keys of <paramref name="newStep"/> whose value differs from <paramref name="previousStep"/>'s;
	/// a key the previous step lacks counts as empty. The single marking comparison.
	/// </summary>
	public static IReadOnlyList<string> ChangedValueKeys(Step previousStep, Step newStep)
	{
		var changedKeys = new List<string>();
		foreach (var (key, value) in newStep.Properties)
		{
			var previousValue = previousStep.Properties.GetValueOrDefault(key, _emptyValue);
			if (!previousValue.Equals(value))
			{
				changedKeys.Add(key.Value);
			}
		}

		return changedKeys;
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

		var oldActive = ActiveColumnSetResolver.Resolve(action, CurrentStep);
		var candidateStep = CurrentStep.WithProperty(columnKey, PropertyValue.FromInt(selectorId));
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
}
