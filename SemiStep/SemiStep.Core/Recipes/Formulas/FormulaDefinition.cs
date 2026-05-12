namespace SemiStep.Core.Recipes.Formulas;

public sealed record FormulaDefinition(
	string Expression,
	IReadOnlyList<string> RecalcOrder);
