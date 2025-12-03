using Core.Reasons.Errors;

using FluentResults;

namespace Core.Formulas;

/// <summary>
/// Represents a compiled formula that can efficiently recalculate variables based on changes.
/// </summary>
public sealed class CompiledFormula
{
	private readonly IReadOnlyList<string> _recalcOrder;
	private readonly IReadOnlyList<string> _variables;
	private readonly IReadOnlyDictionary<string, Func<Dictionary<string, double>, double>> _solvers;

	/// <summary>
	/// Initializes a new instance of the <see cref="CompiledFormula"/> class.
	/// </summary>
	/// <param name="recalcOrder">The order in which variables should be recalculated.</param>
	/// <param name="variables">The list of all variables in the formula.</param>
	/// <param name="solvers">Dictionary of solver functions for each variable.</param>
	public CompiledFormula(
		IReadOnlyList<string> recalcOrder,
		IReadOnlyList<string> variables,
		IReadOnlyDictionary<string, Func<Dictionary<string, double>, double>> solvers)
	{
		_recalcOrder = recalcOrder;
		_variables = variables;
		_solvers = solvers;
	}

	/// <summary>
	/// Applies recalculation based on a changed variable value.
	/// </summary>
	/// <param name="changedVariable">The name of the variable that was changed.</param>
	/// <param name="currentValues">The current values of all variables.</param>
	/// <returns>A dictionary containing the changed variable and the recalculated target variable.</returns>
	public Result<IReadOnlyDictionary<string, double>> ApplyRecalculation(
		string changedVariable,
		IReadOnlyDictionary<string, double> currentValues)
	{
		var preparationResult = PrepareCalculation(changedVariable);
		if (preparationResult.IsFailed)
			return preparationResult.ToResult<IReadOnlyDictionary<string, double>>();

		var targetVariable = preparationResult.Value;

		var computationResult = ComputeTargetValue(targetVariable, currentValues);
		if (computationResult.IsFailed)
			return computationResult.ToResult<IReadOnlyDictionary<string, double>>();

		return CreateCalculationResult(changedVariable, currentValues[changedVariable], targetVariable,
			computationResult.Value);
	}


	private Result<string> PrepareCalculation(
		string changedVariable)
	{
		var validationResult = EnsureVariableIsKnown(changedVariable);
		if (validationResult.IsFailed)
			return validationResult.ToResult<string>();

		var targetVariable = DetermineTarget(changedVariable);
		if (targetVariable == null)
			return new CoreFormulaTargetNotFoundError();

		return Result.Ok(targetVariable);
	}

	private Result EnsureVariableIsKnown(string variableName)
	{
		return _variables.Contains(variableName, StringComparer.OrdinalIgnoreCase)
			? Result.Ok()
			: new CoreFormulaVariableUnknownError(variableName);
	}

	private string? DetermineTarget(string changedVariable)
	{
		return _recalcOrder.FirstOrDefault(v => !string.Equals(v, changedVariable, StringComparison.OrdinalIgnoreCase));
	}

	private Result<double> ComputeTargetValue(string targetVariable, IReadOnlyDictionary<string, double> values)
	{
		if (!_solvers.TryGetValue(targetVariable, out var solver))
			return new CoreFormulaTargetNotFoundError();

		try
		{
			var calculatedValue = solver((Dictionary<string, double>)values);
			return ValidateCalculationResult(calculatedValue);
		}
		catch (CannotEvalException ex)
		{
			return new CoreFormulaComputationFailedError(ex.Message);
		}
		catch (DivideByZeroException)
		{
			return new CoreFormulaDivisionByZeroError(targetVariable);
		}
		catch (Exception ex)
		{
			return new CoreFormulaComputationFailedError(ex.Message);
		}
	}

	private static Result<double> ValidateCalculationResult(double value)
	{
		return double.IsNaN(value) || double.IsInfinity(value)
			? new CoreFormulaComputationFailedError("Result is NaN or Infinity")
			: value;
	}

	private static Result<IReadOnlyDictionary<string, double>> CreateCalculationResult(
		string changedVariable,
		double changedValue,
		string targetVariable,
		double targetValue)
	{
		return Result.Ok<IReadOnlyDictionary<string, double>>(
			new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
			{
				[changedVariable] = changedValue,
				[targetVariable] = targetValue
			});
	}

	/// <summary>
	/// Returns a string representation of the compiled formula showing its variables and recalculation order.
	/// </summary>
	public override string ToString()
	{
		var variablesStr = string.Join(", ", _variables);
		var orderStr = string.Join(" -> ", _recalcOrder);
		return $"Variables: [{variablesStr}], Order: [{orderStr}]";
	}
}
