namespace SemiStep.Core.Recipes.Formulas;

internal sealed record FormulaDefinition(
	string Expression,
	IReadOnlyList<string> RecalcOrder);
