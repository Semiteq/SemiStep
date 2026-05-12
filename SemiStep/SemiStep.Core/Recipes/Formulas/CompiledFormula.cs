using FluentResults;

namespace SemiStep.Core.Recipes.Formulas;

public sealed class CompiledFormula(
	IReadOnlyList<string> recalcOrder,
	IReadOnlyList<string> variables,
	IReadOnlyDictionary<string, Func<Dictionary<string, double>, double>> solvers)
{
	public Result<Dictionary<string, double>> ApplyRecalculation(
		string changedVariable,
		IReadOnlyDictionary<string, double> currentValues)
	{
		if (!IsVariableKnown(changedVariable))
		{
			return Result.Fail(
				$"Variable '{changedVariable}' is not defined in formula");
		}

		var targetVariable = DetermineTarget(changedVariable);
		if (targetVariable is null)
		{
			return Result.Fail(
				$"No target variable for changed variable '{changedVariable}'");
		}

		var computeResult = ComputeTargetValue(targetVariable, currentValues);
		if (computeResult.IsFailed)
		{
			return computeResult;
		}

		var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
		{
			[changedVariable] = currentValues[changedVariable],
			[targetVariable] = computeResult.Value[targetVariable]
		};

		return result;
	}

	private bool IsVariableKnown(string variableName)
	{
		return variables.Contains(variableName, StringComparer.OrdinalIgnoreCase);
	}

	private string? DetermineTarget(string changedVariable)
	{
		return recalcOrder.FirstOrDefault(
			v => string.Equals(v, changedVariable, StringComparison.OrdinalIgnoreCase));
	}

	private Result<Dictionary<string, double>> ComputeTargetValue(
		string targetVariable,
		IReadOnlyDictionary<string, double> values)
	{
		if (!solvers.TryGetValue(targetVariable, out var solver))
		{
			return Result.Fail(
				$"No solver found for target variable '{targetVariable}'");
		}

		var mutableValues = new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase);
		var calculatedValue = solver(mutableValues);

		if (double.IsNaN(calculatedValue) || double.IsInfinity(calculatedValue))
		{
			return Result.Fail(
				$"Computation for '{targetVariable}' resulted in NaN or Infinity");
		}

		return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
		{
			[targetVariable] = calculatedValue
		};
	}

	public override string ToString()
	{
		var variablesStr = string.Join(", ", variables);
		var orderStr = string.Join(" -> ", recalcOrder);

		return $"Variables: [{variablesStr}], Order: [{orderStr}]";
	}
}
