using System.Collections.Immutable;

using Recipe.Entities;

using Shared.Entities;

namespace Recipe.Services;

public sealed class StepFactory
{
	public Step Create(ActionDefinition action, IReadOnlyList<PropertyDefinition> properties)
	{
		var propertyValues = properties
			.ToImmutableDictionary(
				p => new ColumnId(p.PropertyTypeId),
				CreateDefaultValue);

		return new Step(action.Id.ToString(), propertyValues);
	}

	private static PropertyValue CreateDefaultValue(PropertyDefinition property)
	{
		var type = ParsePropertyType(property.SystemType);
		var defaultValue = GetDefaultForType(type);
		return new PropertyValue(defaultValue, type);
	}

	private static PropertyType ParsePropertyType(string systemType)
	{
		return systemType.ToLowerInvariant() switch
		{
			"int" or "int32" or "integer" => PropertyType.Int,
			"float" or "single" or "double" => PropertyType.Float,
			_ => PropertyType.String
		};
	}

	private static object GetDefaultForType(PropertyType type)
	{
		return type switch
		{
			PropertyType.Int => 0,
			PropertyType.Float => 0.0f,
			_ => string.Empty
		};
	}
}
