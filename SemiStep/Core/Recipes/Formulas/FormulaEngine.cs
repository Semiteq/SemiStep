using FluentResults;

namespace SemiStep.Core.Recipes.Formulas;

internal sealed class FormulaEngine(
	IReadOnlyDictionary<int, CompiledFormula> compiledFormulas)
{
	private readonly IReadOnlyDictionary<int, CompiledFormula> _compiledFormulas =
		compiledFormulas ?? throw new ArgumentNullException(nameof(compiledFormulas));

	public Result<Dictionary<string, double>> Calculate(
		int actionId,
		string changedVariable,
		IReadOnlyDictionary<string, double> currentValues)
	{
		if (!_compiledFormulas.TryGetValue(actionId, out var formula))
		{
			return new Error($"Formula for action ID '{actionId}' was not found");
		}

		return formula.ApplyRecalculation(changedVariable, currentValues);
	}
}
