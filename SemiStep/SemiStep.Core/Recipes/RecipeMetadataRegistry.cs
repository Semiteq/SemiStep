using System.Collections.Concurrent;

using FluentResults;

using SemiStep.Core.Configuration;

namespace SemiStep.Core.Recipes;

public sealed class RecipeMetadataRegistry
{
	private readonly Dictionary<int, ActionDefinition> _actionsById;
	private readonly Dictionary<string, ActionDefinition> _actionsByName;
	private readonly IReadOnlyList<ActionDefinition> _allActions;
	private readonly Dictionary<string, PropertyTypeDefinition> _properties;
	private readonly Dictionary<string, GridColumnDefinition> _columns;
	private readonly IReadOnlyList<GridColumnDefinition> _allColumns;
	private readonly Dictionary<string, GroupDefinition> _groups;

	// Concurrent because RecipeMetadataRegistry is a DI singleton and the typed caches are
	// populated lazily on first access from any consumer.
	private readonly ConcurrentDictionary<string, IReadOnlyList<ComboBoxItemViewModel>> _comboItemsByGroup
		= new(StringComparer.OrdinalIgnoreCase);

	private IReadOnlyList<ComboBoxItemViewModel>? _actionComboBoxItems;

	public RecipeMetadataRegistry(AppConfiguration config)
	{
		_actionsById = new Dictionary<int, ActionDefinition>(config.Actions);

		_actionsByName = new Dictionary<string, ActionDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var action in config.Actions.Values)
		{
			_actionsByName[action.UiName] = action;
		}

		_allActions = config.Actions.Values.ToList();

		_properties = new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, property) in config.Properties)
		{
			_properties[key] = property;
		}

		_columns = new Dictionary<string, GridColumnDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, column) in config.Columns)
		{
			_columns[key] = column;
		}

		_allColumns = config.Columns.Values.ToList();

		_groups = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, group) in config.Groups)
		{
			_groups[key] = group;
		}
	}

	public Result<ActionDefinition> GetAction(int id)
	{
		return TryGetOrFail(_actionsById, id, $"Action with id {id} not found");
	}

	public Result<ActionDefinition> GetActionByName(string name)
	{
		return TryGetOrFail(_actionsByName, name, $"Action with name '{name}' not found");
	}

	public Result ActionExists(int id)
	{
		return ContainsOrFail(_actionsById, id, $"Action with id {id} not found");
	}

	public Result ActionExistsByName(string name)
	{
		return ContainsOrFail(_actionsByName, name, $"Action with name '{name}' not found");
	}

	public IReadOnlyList<ActionDefinition> GetAllActions()
	{
		return _allActions;
	}

	public Result<PropertyTypeDefinition> GetProperty(string propertyTypeId)
	{
		return TryGetOrFail(_properties, propertyTypeId, $"Property '{propertyTypeId}' not found");
	}

	public Result PropertyExists(string propertyTypeId)
	{
		return ContainsOrFail(_properties, propertyTypeId, $"Property '{propertyTypeId}' not found");
	}

	public Result<GridColumnDefinition> GetColumn(string key)
	{
		return TryGetOrFail(_columns, key, $"Column '{key}' not found");
	}

	public Result ColumnExists(string key)
	{
		return ContainsOrFail(_columns, key, $"Column '{key}' not found");
	}

	public IReadOnlyList<GridColumnDefinition> GetAllColumns()
	{
		return _allColumns;
	}

	public Result<GroupDefinition> GetGroup(string groupId)
	{
		return TryGetOrFail(_groups, groupId, $"Group '{groupId}' not found");
	}

	public Result GroupExists(string groupId)
	{
		return ContainsOrFail(_groups, groupId, $"Group '{groupId}' not found");
	}

	/// <summary>
	/// Returns the cached list of ComboBox items for the given group name. Diverges from the
	/// Result&lt;T&gt; pattern used elsewhere in this class: returns Array.Empty when the group
	/// is unknown so that UI bindings (per-row dictionaries on RecipeRowViewModel) can hold an
	/// empty-but-valid reference without surfacing a failure to the binding pipeline.
	/// </summary>
	public IReadOnlyList<ComboBoxItemViewModel> GetComboBoxItems(string groupName)
	{
		ArgumentNullException.ThrowIfNull(groupName);

		return _comboItemsByGroup.GetOrAdd(groupName, static (key, self) =>
		{
			var groupResult = self.GetGroup(key);
			if (groupResult.IsFailed)
			{
				return Array.Empty<ComboBoxItemViewModel>();
			}

			return groupResult.Value.Items
				.Select(entry => new ComboBoxItemViewModel(entry.Key, entry.Value))
				.OrderBy(item => item.Id)
				.ToList();
		}, this);
	}

	/// <summary>
	/// Returns the cached list of ComboBox items for all actions. Mirrors GetComboBoxItems for
	/// the action ComboBox column so the cell factory does not need its own cache layer.
	/// </summary>
	public IReadOnlyList<ComboBoxItemViewModel> GetActionComboBoxItems()
	{
		return _actionComboBoxItems ??= _allActions
			.Select(action => new ComboBoxItemViewModel(action.Id, action.UiName))
			.ToList();
	}

	public Result GroupHasIntKey(int key, string groupId)
	{
		var groupResult = GetGroup(groupId);
		if (groupResult.IsFailed)
		{
			return groupResult.ToResult();
		}

		if (!groupResult.Value.Items.ContainsKey(key))
		{
			return Result.Fail($"Value {key} is not a valid member of group '{groupId}'");
		}

		return Result.Ok();
	}

	private static Result<TValue> TryGetOrFail<TKey, TValue>(
		Dictionary<TKey, TValue> dictionary,
		TKey key,
		string errorMessage) where TKey : notnull
	{
		if (dictionary.TryGetValue(key, out var value))
		{
			return value;
		}

		return Result.Fail(errorMessage);
	}

	private static Result ContainsOrFail<TKey, TValue>(
		Dictionary<TKey, TValue> dictionary,
		TKey key,
		string errorMessage) where TKey : notnull
	{
		if (dictionary.ContainsKey(key))
		{
			return Result.Ok();
		}

		return Result.Fail(errorMessage);
	}
}
