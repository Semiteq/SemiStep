using NCalc.Domain;

namespace SemiStep.Core.Recipes.Formulas;

public sealed class FormulaDefinition
{
	public FormulaDefinition(
		IReadOnlyList<string> recalcOrder,
		IReadOnlyDictionary<string, string> expressionSources,
		IReadOnlyDictionary<string, LogicalExpression> compiledExpressions)
	{
		RecalcOrder = recalcOrder;
		ExpressionSources = expressionSources;
		CompiledExpressions = compiledExpressions;
	}

	public IReadOnlyList<string> RecalcOrder { get; }

	public IReadOnlyDictionary<string, string> ExpressionSources { get; }

	public IReadOnlyDictionary<string, LogicalExpression> CompiledExpressions { get; }
}
