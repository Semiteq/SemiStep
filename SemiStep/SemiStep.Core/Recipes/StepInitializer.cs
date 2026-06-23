using System.Collections.Immutable;
using System.Globalization;

using SemiStep.Core.Recipes.Helpers;

namespace SemiStep.Core.Recipes;

public static class StepInitializer
{
	internal static Step Create(
		ActionDefinition action,
		RecipeMetadataRegistry recipeMetadataRegistry)
	{
		// Seed to a fixpoint. Always-active columns (no activation conditions — this includes
		// every top-level selector) are seeded first so the active set can be computed; each
		// newly-seeded selector can in turn activate deeper columns, so we recompute the active
		// set and seed again until it stops growing. For a flat (non-nested) action every column
		// is always-active, so this converges in a single pass and behaves exactly as before.
		// Inactive columns (e.g. capacitor columns under an Авто selector) are deliberately left
		// out so serialization writes their PLC slot as 0/empty instead of a stale default.
		var properties = ImmutableDictionary<PropertyId, PropertyValue>.Empty;

		var alwaysActive = action.Properties
			.Where(column => column.Activation is null || column.Activation.Count == 0);
		foreach (var column in alwaysActive)
		{
			properties = properties.SetItem(
				new PropertyId(column.Key),
				ResolveDefaultValue(column, recipeMetadataRegistry));
		}

		// Active set grows monotonically (seeding a selector can only add columns). The loop
		// terminates when a pass seeds nothing (seededThisPass == false) — that is the real
		// termination mechanism, reached once the active set stops growing. The Properties.Count
		// bound is only a defensive upper limit against an unforeseen non-convergence; it can never
		// actually be hit first, because the active set can grow at most Properties.Count times.
		for (var iteration = 0; iteration <= action.Properties.Count; iteration++)
		{
			var partialStep = new Step(action.Id, properties);
			var activeColumnKeys = ActiveColumnSetResolver.Resolve(action, partialStep);

			var seededThisPass = false;
			foreach (var column in action.Properties)
			{
				if (properties.ContainsKey(new PropertyId(column.Key)) || !activeColumnKeys.Contains(column.Key))
				{
					continue;
				}

				properties = properties.SetItem(
					new PropertyId(column.Key),
					ResolveDefaultValue(column, recipeMetadataRegistry));
				seededThisPass = true;
			}

			if (!seededThisPass)
			{
				break;
			}
		}

		return new Step(action.Id, properties);
	}

	// Config registries are pre-validated at startup; .Value access is safe here.
	internal static PropertyValue ResolveDefaultValue(
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
