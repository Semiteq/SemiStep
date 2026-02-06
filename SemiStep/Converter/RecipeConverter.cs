using System.Collections.Immutable;

using Core.Entities;

using Shared.Entities;
using Shared.Registries;

namespace Converter;

public sealed class RecipeConverter
{
	private readonly IActionRegistry _actions;
	private readonly IPropertyRegistry _properties;

	public RecipeConverter(IActionRegistry actions, IPropertyRegistry properties)
	{
		_actions = actions;
		_properties = properties;
	}

	public PlcRecipeData FromRecipe(Recipe recipe)
	{
		if (recipe.StepCount == 0)
		{
			return PlcRecipeData.Empty;
		}

		var intValues = new List<int>();
		var floatValues = new List<float>();
		var stringValues = new List<string>();

		foreach (var step in recipe.Steps)
		{
			intValues.Add(step.ActionKey);

			var action = _actions.GetAction(step.ActionKey);

			foreach (var columnDef in action.Columns)
			{
				var columnId = new ColumnId(columnDef.Key);
				var propertyDef = _properties.GetProperty(columnDef.PropertyTypeId);
				var propertyType = ParseSystemType(propertyDef.SystemType);

				if (!step.Properties.TryGetValue(columnId, out var propertyValue))
				{
					AppendDefaultValue(propertyType, intValues, floatValues, stringValues);
					continue;
				}

				switch (propertyType)
				{
					case PropertyType.Int:
						intValues.Add(propertyValue.AsInt());
						break;
					case PropertyType.Float:
						floatValues.Add(propertyValue.AsFloat());
						break;
					case PropertyType.String:
						stringValues.Add(propertyValue.AsString());
						break;
				}
			}
		}

		return new PlcRecipeData(
			IntValues: intValues.ToArray(),
			FloatValues: floatValues.ToArray(),
			StringValues: stringValues.ToArray(),
			StepCount: recipe.StepCount);
	}

	public Recipe ToRecipe(PlcRecipeData data)
	{
		if (data.StepCount == 0)
		{
			return Recipe.Empty;
		}

		var intIndex = 0;
		var floatIndex = 0;
		var stringIndex = 0;

		var steps = new List<Step>(data.StepCount);

		for (var stepIndex = 0; stepIndex < data.StepCount; stepIndex++)
		{
			if (intIndex >= data.IntValues.Length)
			{
				throw new InvalidOperationException(
					$"Insufficient int values at step {stepIndex}: expected ActionKey but reached end of array");
			}

			var actionKey = data.IntValues[intIndex++];
			var action = _actions.GetAction(actionKey);

			var properties = ImmutableDictionary.CreateBuilder<ColumnId, PropertyValue>();

			foreach (var columnDef in action.Columns)
			{
				var columnId = new ColumnId(columnDef.Key);
				var propertyDef = _properties.GetProperty(columnDef.PropertyTypeId);
				var propertyType = ParseSystemType(propertyDef.SystemType);

				PropertyValue propertyValue;

				switch (propertyType)
				{
					case PropertyType.Int:
						if (intIndex >= data.IntValues.Length)
						{
							throw new InvalidOperationException(
								$"Insufficient int values at step {stepIndex}, column '{columnDef.Key}'");
						}
						propertyValue = new PropertyValue(data.IntValues[intIndex++], PropertyType.Int);
						break;

					case PropertyType.Float:
						if (floatIndex >= data.FloatValues.Length)
						{
							throw new InvalidOperationException(
								$"Insufficient float values at step {stepIndex}, column '{columnDef.Key}'");
						}
						propertyValue = new PropertyValue(data.FloatValues[floatIndex++], PropertyType.Float);
						break;

					case PropertyType.String:
						if (stringIndex >= data.StringValues.Length)
						{
							throw new InvalidOperationException(
								$"Insufficient string values at step {stepIndex}, column '{columnDef.Key}'");
						}
						propertyValue = new PropertyValue(data.StringValues[stringIndex++], PropertyType.String);
						break;

					default:
						throw new InvalidOperationException($"Unknown property type: {propertyType}");
				}

				properties.Add(columnId, propertyValue);
			}

			steps.Add(new Step(actionKey, properties.ToImmutable()));
		}

		return new Recipe(steps.ToImmutableList());
	}

	private static PropertyType ParseSystemType(string systemType)
	{
		return systemType.ToLowerInvariant() switch
		{
			"int" => PropertyType.Int,
			"float" => PropertyType.Float,
			"string" => PropertyType.String,
			_ => throw new InvalidOperationException($"Unknown system type: {systemType}")
		};
	}

	private static void AppendDefaultValue(
		PropertyType type,
		List<int> intValues,
		List<float> floatValues,
		List<string> stringValues)
	{
		switch (type)
		{
			case PropertyType.Int:
				intValues.Add(0);
				break;
			case PropertyType.Float:
				floatValues.Add(0f);
				break;
			case PropertyType.String:
				stringValues.Add(string.Empty);
				break;
		}
	}
}
