using Core.Entities;
using Core.Properties;
using Core.Reasons.Errors;

using FluentResults;

namespace Core.Formulas;

public sealed class StepVariableAdapter : IStepVariableAdapter
{
	public Result<IReadOnlyDictionary<string, double>> ExtractVariables(Step step, IReadOnlyList<string> variableNames)
	{
		var values = new Dictionary<string, double>(variableNames.Count, StringComparer.OrdinalIgnoreCase);

		foreach (var variableName in variableNames)
		{
			var columnKey = new ColumnIdentifier(variableName);
			var propertyResult = step.GetProperty(columnKey);
			if (propertyResult.IsFailed)
				return new CoreFormulaVariableNotFoundError(variableName);

			var doubleValueResult = propertyResult.Value.GetNumeric();
			if (doubleValueResult.IsFailed)
				return new CoreFormulaVariableNonNumericError(variableName);

			values[variableName] = doubleValueResult.Value;
		}

		return values;
	}

	public Result<Step> ApplyChanges(Step originalStep, IReadOnlyDictionary<string, double> variableUpdates)
	{
		var updatedProperties = originalStep.Properties;

		foreach (var variableUpdate in variableUpdates)
		{
			var columnKey = new ColumnIdentifier(variableUpdate.Key);

			if (!originalStep.Properties.TryGetValue(columnKey, out var oldProperty) || oldProperty == null)
				continue;

			var convertedValue = ConvertFromDouble(variableUpdate.Value, oldProperty);
			if (convertedValue.IsFailed)
				return convertedValue.ToResult();

			var newPropertyResult = oldProperty.WithValue(convertedValue.Value);
			if (newPropertyResult.IsFailed)
				return newPropertyResult.ToResult<Step>();

			updatedProperties = updatedProperties.SetItem(columnKey, newPropertyResult.Value);
		}

		return originalStep with { Properties = updatedProperties };
	}

	private static Result<object> ConvertFromDouble(double value, Property originalProperty)
	{
		return originalProperty.GetValueAsObject switch
		{
			short => (short)Math.Round(value),
			float => (float)value,
			_ => new CorePropertyNonNumericError()
		};
	}
}
