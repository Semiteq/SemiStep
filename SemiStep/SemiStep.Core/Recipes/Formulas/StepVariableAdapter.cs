using FluentResults;

namespace SemiStep.Core.Recipes.Formulas;

internal static class StepVariableAdapter
{
	public static Result<IReadOnlyDictionary<string, double>> ExtractVariables(
		Step step,
		IReadOnlyList<string> variableNames)
	{
		var values = new Dictionary<string, double>(variableNames.Count, StringComparer.OrdinalIgnoreCase);

		foreach (var variableName in variableNames)
		{
			var columnId = new PropertyId(variableName);

			if (!step.Properties.TryGetValue(columnId, out var propertyValue))
			{
				return Result.Fail<IReadOnlyDictionary<string, double>>(
					$"Variable '{variableName}' not found in step properties");
			}

			var numericResult = ToDouble(propertyValue);
			if (numericResult.IsFailed)
			{
				return numericResult.ToResult<IReadOnlyDictionary<string, double>>();
			}

			values[variableName] = numericResult.Value;
		}

		return Result.Ok<IReadOnlyDictionary<string, double>>(values);
	}

	public static Result<Step> ApplyChanges(
		Step originalStep,
		IReadOnlyDictionary<string, double> variableUpdates)
	{
		var propertyUpdates = new List<KeyValuePair<PropertyId, PropertyValue>>();

		foreach (var (variableName, formulaValue) in variableUpdates)
		{
			var columnId = new PropertyId(variableName);

			if (!originalStep.Properties.TryGetValue(columnId, out var existingProperty))
			{
				continue;
			}

			var convertResult = FromDouble(formulaValue, existingProperty.Type);
			if (convertResult.IsFailed)
			{
				return convertResult.ToResult<Step>();
			}

			propertyUpdates.Add(KeyValuePair.Create(columnId, convertResult.Value));
		}

		var updatedStep = originalStep with
		{
			Properties = originalStep.Properties.SetItems(propertyUpdates)
		};

		return Result.Ok(updatedStep);
	}

	private static Result<double> ToDouble(PropertyValue value)
	{
		return value.Value switch
		{
			int i => i,
			float f => f,
			_ => Result.Fail<double>($"Cannot convert value '{value.Value}' to numeric")
		};
	}

	private static Result<PropertyValue> FromDouble(double value, PropertyType targetType)
	{
		return targetType switch
		{
			PropertyType.Int => PropertyValue.FromInt((int)Math.Round(value)),
			PropertyType.Float => PropertyValue.FromFloat((float)value),
			_ => Result.Fail<PropertyValue>(
				$"Cannot convert formula result to property type '{targetType}'")
		};
	}
}
