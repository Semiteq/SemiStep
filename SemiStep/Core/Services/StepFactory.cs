using System.Collections.Immutable;

using Core.Entities;

using Shared.Entities;
using Shared.Registries;

namespace Core.Services;

public sealed class StepFactory
{
	public static Step Create(
		ActionDefinition action,
		IPropertyRegistry propertyRegistry,
		IGroupRegistry groupRegistry)
	{
		var propertyValues = action.Columns
			.ToImmutableDictionary(
				col => new ColumnId(col.Key),
				col => ResolveValue(col, propertyRegistry, groupRegistry));

		return new Step(action.Id, propertyValues);
	}

	private static PropertyValue ResolveValue(
		ActionColumnDefinition column,
		IPropertyRegistry propertyRegistry,
		IGroupRegistry groupRegistry)
	{
		var propertyDefinition = propertyRegistry.GetProperty(column.PropertyTypeId);
		var propertyType = ParsePropertyType(propertyDefinition.SystemType);

		if (!string.IsNullOrEmpty(column.DefaultValue))
		{
			return ParseValue(column.DefaultValue, propertyType);
		}

		if (column.GroupName is not null && groupRegistry.GroupExists(column.GroupName))
		{
			var group = groupRegistry.GetGroup(column.GroupName);
			if (group.Items.Count > 0)
			{
				var firstKey = group.Items.Keys.Min();
				return new PropertyValue(firstKey, PropertyType.Int);
			}
		}

		return new PropertyValue(GetDefaultForType(propertyType), propertyType);
	}

	private static PropertyValue ParseValue(string rawValue, PropertyType propertyType)
	{
		return propertyType switch
		{
			PropertyType.Int when int.TryParse(rawValue, out var intResult)
				=> new PropertyValue(intResult, PropertyType.Int),
			PropertyType.Float when float.TryParse(
				rawValue,
				System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture,
				out var floatResult)
				=> new PropertyValue(floatResult, PropertyType.Float),
			_ => new PropertyValue(rawValue, PropertyType.String)
		};
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
