using Core.ConfigImport.Dto;
using Core.Definitions;
using Core.Properties.Contracts;

using FluentResults;

namespace Core.ConfigImport.Runtime;

internal sealed class CoreConfigBuilder
{
	internal Result<BuiltCoreConfig> Build(ConfigRootDto dto, IReadOnlyDictionary<string, IPropertyDefinition> propertyTypeRegistry)
	{
		if (dto is null)
			return Result.Fail("ConfigRootDto is null.");
		if (dto.Actions is null || dto.Actions.Count == 0)
			return Result.Fail("No actions defined.");
		if (propertyTypeRegistry is null)
			throw new ArgumentNullException(nameof(propertyTypeRegistry));

		var actionsByKey = new Dictionary<string, ActionDefinition>(StringComparer.Ordinal);
		var schema = new Dictionary<(string ActionKey, string ColumnId), IPropertyDefinition>();

		foreach (var action in dto.Actions)
		{
			if (string.IsNullOrWhiteSpace(action.Key))
				return Result.Fail("Action key is empty.");

			if (actionsByKey.ContainsKey(action.Key))
				return Result.Fail("Duplicate action key.");

			actionsByKey[action.Key] = new ActionDefinition(action.Key, action.InternalId);

			foreach (var property in action.Properties)
			{
				if (string.IsNullOrWhiteSpace(property.ColumnId))
					return Result.Fail("ColumnId is empty.");

				if (!propertyTypeRegistry.TryGetValue(property.PropertyTypeId, out var definition))
					return Result.Fail("PropertyTypeId not found in registry.");

				schema[(action.Key, property.ColumnId)] = definition;
			}
		}

		return Result.Ok(new BuiltCoreConfig(actionsByKey, schema));
	}
}
