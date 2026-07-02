using FluentResults;

using NCalc;

namespace SemiStep.Core.Recipes.Formulas;

public static class FormulaIdentifierExtractor
{
	public static Result<(LogicalExpression LogicalExpression, IReadOnlySet<string> Identifiers)> Parse(string source)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			return Result.Fail<(LogicalExpression, IReadOnlySet<string>)>("Expression source is empty.");
		}

		try
		{
			var expression = new Expression(source);
			// GetParameterNames forces parsing and populates LogicalExpression as a side effect.
			var parameterNames = expression.GetParameterNames();
			// Ordinal comparer preserves case-only duplicates so the mapper can detect casing
			// mismatches between identifiers and recalc_order entries.
			var identifiers = new HashSet<string>(parameterNames, StringComparer.Ordinal);

			return Result.Ok<(LogicalExpression, IReadOnlySet<string>)>(
				(expression.LogicalExpression!, identifiers));
		}
		catch (Exception parseException)
		{
			return Result.Fail<(LogicalExpression, IReadOnlySet<string>)>(
				$"Failed to parse expression '{source}': {parseException.Message}");
		}
	}
}
