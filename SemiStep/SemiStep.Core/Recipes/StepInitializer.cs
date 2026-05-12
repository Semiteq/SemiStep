using System.Collections.Immutable;
using System.Globalization;

namespace SemiStep.Core.Recipes;

internal static class StepInitializer
{
	internal static Step Create(
		ActionDefinition action,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var propertyValues = action.Properties
			.ToImmutableDictionary(
				col => new PropertyId(col.Key),
				col => ResolveDefaultValue(col, recipeMetadataRegistry));

		return new Step(action.Id, propertyValues);
	}

	// Config registries are pre-validated at startup; .Value access is safe here.
	private static PropertyValue ResolveDefaultValue(
		ActionPropertyDefinition property,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		var propertyDefinition = recipeMetadataRegistry.GetProperty(property.PropertyTypeId).Value;
		var propertyType = PropertyTypeMapping.FromSystemType(propertyDefinition.SystemType);

		if (!string.IsNullOrEmpty(property.DefaultValue))
		{
			return ParseDefaultValue(property.DefaultValue, propertyType)
				   ?? PropertyValue.FromString(property.DefaultValue);
		}

		if (property.GroupName is not null && recipeMetadataRegistry.GroupExists(property.GroupName).IsSuccess)
		{
			var group = recipeMetadataRegistry.GetGroup(property.GroupName).Value;
			if (group.Items.Count > 0)
			{
				return PropertyValue.FromInt(group.Items.Keys.Min());
			}
		}

		return GetZeroValue(propertyType);
	}

	private static PropertyValue? ParseDefaultValue(string rawValue, PropertyType targetType)
	{
		return targetType switch
		{
			PropertyType.Int => int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intResult)
				? PropertyValue.FromInt(intResult)
				: null,
			PropertyType.Float => float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatResult)
				? PropertyValue.FromFloat(floatResult)
				: null,
			PropertyType.String => PropertyValue.FromString(rawValue),
			_ => null
		};
	}

	private static PropertyValue GetZeroValue(PropertyType type)
	{
		return type switch
		{
			PropertyType.Int => PropertyValue.FromInt(0),
			PropertyType.Float => PropertyValue.FromFloat(0f),
			_ => PropertyValue.FromString("")
		};
	}
}
