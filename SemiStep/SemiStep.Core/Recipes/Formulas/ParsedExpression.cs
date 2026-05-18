using NCalc.Domain;

namespace SemiStep.Core.Recipes.Formulas;

public sealed record ParsedExpression(
	LogicalExpression LogicalExpression,
	IReadOnlySet<string> Identifiers);
