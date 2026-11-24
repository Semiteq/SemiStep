using System.Diagnostics.CodeAnalysis;

using FluentResults;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes.Errors;

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

	private readonly int _stringMaxLength;

	public RecipeMetadataRegistry(AppConfiguration config)
	{
		var resolvedActions = ResolvePrimaryActions([.. config.Actions.Values]);

		_actionsById = [];
		_actionsByName = [with(StringComparer.OrdinalIgnoreCase)];
		foreach (var action in resolvedActions)
		{
			_actionsById[action.Id] = action;
			_actionsByName[action.UiName] = action;
		}

		_allActions = resolvedActions;

		_properties = [with(StringComparer.OrdinalIgnoreCase)];
		foreach (var (key, property) in config.Properties)
		{
			_properties[key] = property;
		}

		_columns = [with(StringComparer.OrdinalIgnoreCase)];
		foreach (var (key, column) in config.Columns)
		{
			_columns[key] = column;
		}

		_allColumns = [.. config.Columns.Values];

		_groups = [with(StringComparer.OrdinalIgnoreCase)];
		foreach (var (key, group) in config.Groups)
		{
			_groups[key] = group;
		}

		_stringMaxLength = ResolveStringMaxLength(_properties.Values);
	}

	/// <summary>
	/// Runs the reference-graph resolver over the raw mapped actions so the registry holds only
	/// resolved primary actions. Subactions (<see cref="ActionRole.Subaction"/>) are consumed by
	/// the resolver and never enter the runtime action collections; each surviving primary carries
	/// the union of its own and its subactions' columns (deterministic order) as its
	/// <see cref="ActionDefinition.Properties"/>. Resolver failures (cycles, dangling targets,
	/// conflicting column types) fail fast at construction.
	/// </summary>
	private static IReadOnlyList<ActionDefinition> ResolvePrimaryActions(
		IReadOnlyCollection<ActionDefinition> rawActions)
	{
		var resolveResult = ActionTreeResolver.Resolve(rawActions);
		return resolveResult.IsFailed
			? throw new InvalidOperationException(
				"RecipeMetadataRegistry: failed to resolve the action reference graph: "
				+ string.Join("; ", resolveResult.Errors.Select(error => error.Message)))
			: resolveResult.Value;
	}

	public Result<ActionDefinition> GetAction(int id)
	{
		return TryGetOrFail(_actionsById, id, new ActionByIdNotFoundError(id));
	}

	public bool TryGetAction(int id, [MaybeNullWhen(false)] out ActionDefinition action)
	{
		return _actionsById.TryGetValue(id, out action);
	}

	public Result<ActionDefinition> GetActionByName(string name)
	{
		return TryGetOrFail(_actionsByName, name, new ActionByNameNotFoundError(name));
	}

	public Result ActionExists(int id)
	{
		return ContainsOrFail(_actionsById, id, new ActionByIdNotFoundError(id));
	}

	public Result ActionExistsByName(string name)
	{
		return ContainsOrFail(_actionsByName, name, new ActionByNameNotFoundError(name));
	}

	public IReadOnlyList<ActionDefinition> GetAllActions()
	{
		return _allActions;
	}

	public Result<PropertyTypeDefinition> GetProperty(string propertyTypeId)
	{
		return TryGetOrFail(_properties, propertyTypeId, new PropertyNotFoundError(propertyTypeId));
	}

	public Result PropertyExists(string propertyTypeId)
	{
		return ContainsOrFail(_properties, propertyTypeId, new PropertyNotFoundError(propertyTypeId));
	}

	public Result<GridColumnDefinition> GetColumn(string key)
	{
		return TryGetOrFail(_columns, key, new ColumnNotFoundError(key));
	}

	public Result ColumnExists(string key)
	{
		return ContainsOrFail(_columns, key, new ColumnNotFoundError(key));
	}

	public IReadOnlyList<GridColumnDefinition> GetAllColumns()
	{
		return _allColumns;
	}

	public Result<GroupDefinition> GetGroup(string groupId)
	{
		return TryGetOrFail(_groups, groupId, new GroupNotFoundError(groupId));
	}

	public Result GroupExists(string groupId)
	{
		return ContainsOrFail(_groups, groupId, new GroupNotFoundError(groupId));
	}

	/// <summary>
	/// Single source of truth for recipe string max_length; ConfigFacade validates the rules behind it.
	/// </summary>
	public int GetStringMaxLength()
	{
		return _stringMaxLength;
	}

	private static int ResolveStringMaxLength(IEnumerable<PropertyTypeDefinition> properties)
	{
		var maxLengths = properties
			.Where(property => string.Equals(property.SystemType, "string", StringComparison.OrdinalIgnoreCase))
			.Select(property => property.MaxLength)
			.Distinct()
			.ToList();

		return maxLengths is [int maxLength] && maxLength > 0
			? maxLength
			: throw new InvalidOperationException(
				"RecipeMetadataRegistry: string properties must share one positive max_length.");
	}

	public Result GroupHasIntKey(int key, string groupId)
	{
		var groupResult = GetGroup(groupId);
		return groupResult.IsFailed
			? groupResult.ToResult()
			: !groupResult.Value.Items.ContainsKey(key) ? Result.Fail(new ValueNotInGroupError(key, groupId)) : Result.Ok();
	}

	private static Result<TValue> TryGetOrFail<TKey, TValue>(
		Dictionary<TKey, TValue> dictionary,
		TKey key,
		IError error) where TKey : notnull
	{
		return dictionary.TryGetValue(key, out var value) ? (Result<TValue>)value : (Result<TValue>)Result.Fail(error);
	}

	private static Result ContainsOrFail<TKey, TValue>(
		Dictionary<TKey, TValue> dictionary,
		TKey key,
		IError error) where TKey : notnull
	{
		return dictionary.ContainsKey(key) ? Result.Ok() : Result.Fail(error);
	}
}
