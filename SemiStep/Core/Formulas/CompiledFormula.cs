using Core.Exceptions;

namespace Core.Formulas;

public sealed class CompiledFormula(
	IReadOnlyList<string> recalcOrder,
	IReadOnlyList<string> variables,
	IReadOnlyDictionary<string, Func<Dictionary<string, double>, double>> solvers)
{
	public Dictionary<string, double> ApplyRecalculation(
		string changedVariable,
		IReadOnlyDictionary<string, double> currentValues)
	{
		if (!IsVariableKnown(changedVariable))
		{
			throw new FormulaVariableNotFoundException(
				$"Could not find variable '{changedVariable}' in step properties.");
		}

		var targetVariable = DetermineTarget(changedVariable);
		if (targetVariable is null)
		{
			throw new FormulaNoTargetVariableException(
				$"No target variable could be determined for changed variable '{changedVariable}'.");
		}

		var computeResult = ComputeTargetValue(targetVariable, currentValues);

		var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
		{
			[changedVariable] = currentValues[changedVariable],
			[targetVariable] = computeResult[targetVariable]
		};

		return result;
	}

	private bool IsVariableKnown(string variableName)
	{
		return variables.Contains(variableName, StringComparer.OrdinalIgnoreCase);
	}

	private string? DetermineTarget(string changedVariable)
	{
		return recalcOrder.FirstOrDefault(v => string.Equals(v, changedVariable, StringComparison.OrdinalIgnoreCase));
	}

	private Dictionary<string, double> ComputeTargetValue(string targetVariable,
		IReadOnlyDictionary<string, double> values)
	{
		if (!solvers.TryGetValue(targetVariable, out var solver))
		{
			throw new FormulaNoTargetVariableException(
				$"No target variable could be determined for changed variable '{targetVariable}'.");
		}
		var mutableValues = new Dictionary<string, double>(values, StringComparer.OrdinalIgnoreCase);
		var calculatedValue = solver(mutableValues);

		if (double.IsNaN(calculatedValue) || double.IsInfinity(calculatedValue))
		{
			throw new FormulaComputationOverflowException(
				$"Computation for target variable '{targetVariable}' resulted as NaN or Infinity.");
		}

		return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { [targetVariable] = calculatedValue };
	}

	public override string ToString()
	{
		var variablesStr = string.Join(", ", variables);
		var orderStr = string.Join(" -> ", recalcOrder);

		return $"Variables: [{variablesStr}], Order: [{orderStr}]";
	}
}
