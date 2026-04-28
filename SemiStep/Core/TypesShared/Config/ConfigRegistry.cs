using FluentResults;

using TypesShared.Core;

namespace TypesShared.Config;

public sealed class ConfigRegistry
{
	private readonly Dictionary<int, ActionDefinition> _actionsById;
	private readonly Dictionary<string, ActionDefinition> _actionsByName;
	private readonly IReadOnlyList<ActionDefinition> _allActions;
	private readonly Dictionary<string, PropertyTypeDefinition> _properties;
	private readonly Dictionary<string, GridColumnDefinition> _columns;
	private readonly IReadOnlyList<GridColumnDefinition> _allColumns;
	private readonly Dictionary<string, GroupDefinition> _groups;

	public ConfigRegistry(AppConfiguration config)
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

	public Result<PropertyTypeDefinition> ResolvePropertyType(
		Recipe recipe,
		int stepIndex,
		string columnKey)
	{
		var step = recipe.Steps[stepIndex];

		var actionResult = GetAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult<PropertyTypeDefinition>();
		}

		var actionProperty = actionResult.Value.Properties
			.FirstOrDefault(p => p.Key == columnKey);

		if (actionProperty is null)
		{
			return Result.Fail<PropertyTypeDefinition>(
				$"Property '{columnKey}' is not defined in action '{actionResult.Value.UiName}'");
		}

		return GetProperty(actionProperty.PropertyTypeId);
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

		return Result.Fail<TValue>(errorMessage);
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
