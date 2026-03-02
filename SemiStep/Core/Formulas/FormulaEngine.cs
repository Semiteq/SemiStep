using Core.Exceptions;

namespace Core.Formulas;

public sealed class FormulaEngine(
	IReadOnlyDictionary<int, CompiledFormula> compiledFormulas) : IFormulaEngine
{
	private readonly IReadOnlyDictionary<int, CompiledFormula> _compiledFormulas =
		compiledFormulas ?? throw new ArgumentNullException(nameof(compiledFormulas));

	public Dictionary<string, double> Calculate(
		int actionId,
		string changedVariable,
		IReadOnlyDictionary<string, double> currentValues)
	{
		if (!_compiledFormulas.TryGetValue(actionId, out var formula))
		{
			throw new FormulaNotFoundException($"Formula for action ID '{actionId}' was not found.");
		}

		return formula.ApplyRecalculation(changedVariable, currentValues);
	}
}
