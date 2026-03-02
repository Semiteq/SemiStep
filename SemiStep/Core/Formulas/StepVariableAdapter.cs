using Core.Entities;
using Core.Exceptions;

namespace Core.Formulas;

public sealed class StepVariableAdapter : IStepVariableAdapter
{
	public IReadOnlyDictionary<string, double> ExtractVariableNames(Step step, IReadOnlyList<string> variableNames)
	{
		var values = new Dictionary<string, double>(variableNames.Count, StringComparer.OrdinalIgnoreCase);

		foreach (var variableName in variableNames)
		{
			var columnId = new ColumnId(variableName);

			if (!step.Properties.TryGetValue(columnId, out var propertyValue))
			{
				throw new FormulaVariableNotFoundException(
					$"Could not find variable '{variableName}' in step properties.");
			}

			values[variableName] = GetDoubleOrThrow(propertyValue);
		}

		return values;
	}

	public Step ApplyChanges(Step originalStep, IReadOnlyDictionary<string, double> variableUpdates)
	{
		var propertyUpdates = new List<KeyValuePair<ColumnId, PropertyValue>>();

		foreach (var (variableName, formulaValue) in variableUpdates)
		{
			var columnId = new ColumnId(variableName);

			if (!originalStep.Properties.TryGetValue(columnId, out var existingProperty))
			{
				continue;
			}

			var updatedProperty = existingProperty with { Value = ConvertOrThrow(formulaValue, existingProperty.Type) };

			propertyUpdates.Add(KeyValuePair.Create(columnId, updatedProperty));
		}

		var updatedStep = originalStep with { Properties = originalStep.Properties.SetItems(propertyUpdates) };

		return updatedStep;
	}

	private static double GetDoubleOrThrow(PropertyValue value)
	{
		return value.Value switch
		{
			int i => i,
			float f => f,
			_ => throw new TypeMismatchException($"Could not convert value '{value.Value}' to a numeric value.")
		};
	}

	private static object ConvertOrThrow(double formulaValue, PropertyType targetType)
	{
		return targetType switch
		{
			PropertyType.Int => (int)Math.Round(formulaValue),
			PropertyType.Float => (float)formulaValue,
			_ => throw new TypeMismatchException(
				$"Cannot convert formula value to target property type '{targetType}'.")
		};
	}
}
