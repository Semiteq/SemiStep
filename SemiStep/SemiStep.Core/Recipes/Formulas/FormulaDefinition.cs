using NCalc;

namespace SemiStep.Core.Recipes.Formulas;

public sealed class FormulaDefinition(
	IReadOnlyList<string> recalcOrder,
	IReadOnlyDictionary<string, LogicalExpression> compiledExpressions)
{
	public IReadOnlyList<string> RecalcOrder { get; } = recalcOrder;

	public IReadOnlyDictionary<string, LogicalExpression> CompiledExpressions { get; } = compiledExpressions;
}
