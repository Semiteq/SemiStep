using NCalc.Domain;

namespace SemiStep.Core.Recipes.Formulas;

public sealed class FormulaDefinition
{
	public FormulaDefinition(
		IReadOnlyList<string> recalcOrder,
		IReadOnlyDictionary<string, LogicalExpression> compiledExpressions)
	{
		RecalcOrder = recalcOrder;
		CompiledExpressions = compiledExpressions;
	}

	public IReadOnlyList<string> RecalcOrder { get; }

	public IReadOnlyDictionary<string, LogicalExpression> CompiledExpressions { get; }
}
