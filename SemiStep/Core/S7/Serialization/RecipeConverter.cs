using System.Collections.Immutable;

using FluentResults;

using TypesShared.Config;
using TypesShared.Core;
using TypesShared.Plc;

namespace S7.Serialization;

internal sealed class RecipeConverter(ConfigRegistry configRegistry)
{
	public Result<PlcRecipeData> FromRecipe(Recipe recipe)
	{
		if (recipe.StepCount == 0)
		{
			return Result.Ok(PlcRecipeData.Empty);
		}

		var intValues = new List<int>();
		var floatValues = new List<float>();
		var stringValues = new List<string>();

		foreach (var step in recipe.Steps)
		{
			var stepResult = SerialiseStep(step, intValues, floatValues, stringValues);
			if (stepResult.IsFailed)
			{
				return stepResult.ToResult<PlcRecipeData>();
			}
		}

		return Result.Ok(new PlcRecipeData(
			IntValues: intValues.ToArray(),
			FloatValues: floatValues.ToArray(),
			StringValues: stringValues.ToArray(),
			StepCount: recipe.StepCount));
	}

	public Result<Recipe> ToRecipe(PlcRecipeData data)
	{
		if (data.StepCount == 0)
		{
			return Result.Ok(Recipe.Empty);
		}

		var intIndex = 0;
		var floatIndex = 0;
		var stringIndex = 0;

		var steps = new List<Step>(data.StepCount);

		for (var stepIndex = 0; stepIndex < data.StepCount; stepIndex++)
		{
			var stepResult = DeserialiseStep(stepIndex, data, ref intIndex, ref floatIndex, ref stringIndex);
			if (stepResult.IsFailed)
			{
				return stepResult.ToResult<Recipe>();
			}

			steps.Add(stepResult.Value);
		}

		return Result.Ok(new Recipe(steps.ToImmutableList()));
	}

	private Result SerialiseStep(
		Step step,
		List<int> intValues,
		List<float> floatValues,
		List<string> stringValues)
	{
		intValues.Add(step.ActionKey);

		var actionResult = ResolveAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult();
		}

		foreach (var propertyDef in actionResult.Value.Properties)
		{
			step.Properties.TryGetValue(new PropertyId(propertyDef.Key), out var value);
			var propertyResult = SerialiseProperty(propertyDef, value, intValues, floatValues, stringValues);
			if (propertyResult.IsFailed)
			{
				return propertyResult;
			}
		}

		return Result.Ok();
	}

	private Result<Step> DeserialiseStep(
		int stepIndex,
		PlcRecipeData data,
		ref int intIndex,
		ref int floatIndex,
		ref int stringIndex)
	{
		if (intIndex >= data.IntValues.Length)
		{
			return Result.Fail(
				$"Insufficient int values at step {stepIndex}: expected ActionKey but reached end of array");
		}

		var actionKey = data.IntValues[intIndex++];
		var actionResult = ResolveAction(actionKey);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult<Step>();
		}

		var properties = ImmutableDictionary.CreateBuilder<PropertyId, PropertyValue>();

		foreach (var propertyDef in actionResult.Value.Properties)
		{
			var propertyResult = DeserialiseProperty(
				propertyDef, stepIndex, data, ref intIndex, ref floatIndex, ref stringIndex);
			if (propertyResult.IsFailed)
			{
				return propertyResult.ToResult<Step>();
			}

			properties.Add(new PropertyId(propertyDef.Key), propertyResult.Value);
		}

		return Result.Ok(new Step(actionKey, properties.ToImmutable()));
	}

	private Result SerialiseProperty(
		ActionPropertyDefinition propertyDef,
		PropertyValue? value,
		List<int> intValues,
		List<float> floatValues,
		List<string> stringValues)
	{
		var propertyTypeResult = ResolvePropertyType(propertyDef);
		if (propertyTypeResult.IsFailed)
		{
			return propertyTypeResult.ToResult();
		}

		var propertyType = propertyTypeResult.Value;

		if (value is null)
		{
			AppendDefaultValue(propertyType, intValues, floatValues, stringValues);
			return Result.Ok();
		}

		switch (propertyType)
		{
			case PropertyType.Int:
				intValues.Add(value.AsInt());
				break;
			case PropertyType.Float:
				floatValues.Add(value.AsFloat());
				break;
			case PropertyType.String:
				stringValues.Add(value.AsString());
				break;
		}

		return Result.Ok();
	}

	private Result<PropertyValue> DeserialiseProperty(
		ActionPropertyDefinition propertyDef,
		int stepIndex,
		PlcRecipeData data,
		ref int intIndex,
		ref int floatIndex,
		ref int stringIndex)
	{
		var propertyTypeResult = ResolvePropertyType(propertyDef);
		if (propertyTypeResult.IsFailed)
		{
			return propertyTypeResult.ToResult<PropertyValue>();
		}

		switch (propertyTypeResult.Value)
		{
			case PropertyType.Int:
				if (intIndex >= data.IntValues.Length)
				{
					return Result.Fail(
						$"Insufficient int values at step {stepIndex}, column '{propertyDef.Key}'");
				}
				return Result.Ok(PropertyValue.FromInt(data.IntValues[intIndex++]));

			case PropertyType.Float:
				if (floatIndex >= data.FloatValues.Length)
				{
					return Result.Fail(
						$"Insufficient float values at step {stepIndex}, column '{propertyDef.Key}'");
				}
				return Result.Ok(PropertyValue.FromFloat(data.FloatValues[floatIndex++]));

			case PropertyType.String:
				if (stringIndex >= data.StringValues.Length)
				{
					return Result.Fail(
						$"Insufficient string values at step {stepIndex}, column '{propertyDef.Key}'");
				}
				return Result.Ok(PropertyValue.FromString(data.StringValues[stringIndex++]));

			default:
				return Result.Fail($"Unknown property type: {propertyTypeResult.Value}");
		}
	}

	private Result<PropertyType> ResolvePropertyType(ActionPropertyDefinition propertyDef)
	{
		var propertyDefResult = configRegistry.GetProperty(propertyDef.PropertyTypeId);
		if (propertyDefResult.IsFailed)
		{
			return Result.Fail(
				$"Property type '{propertyDef.PropertyTypeId}' not found in configuration registry");
		}

		return Result.Ok(PropertyTypeMapping.FromSystemType(propertyDefResult.Value.SystemType));
	}

	private Result<ActionDefinition> ResolveAction(int actionKey)
	{
		var actionResult = configRegistry.GetAction(actionKey);
		if (actionResult.IsFailed)
		{
			return Result.Fail($"Action with key {actionKey} not found in configuration registry");
		}

		return Result.Ok(actionResult.Value);
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
