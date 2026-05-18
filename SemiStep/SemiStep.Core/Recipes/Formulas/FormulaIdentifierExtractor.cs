using FluentResults;

using NCalc;
using NCalc.Domain;

namespace SemiStep.Core.Recipes.Formulas;

public static class FormulaIdentifierExtractor
{
	public static Result<IReadOnlySet<string>> Extract(string source)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			return Result.Fail<IReadOnlySet<string>>("Expression source is empty.");
		}

		try
		{
			var expression = new Expression(source);
			var parameterNames = expression.GetParameterNames();
			var identifiers = new HashSet<string>(parameterNames, StringComparer.OrdinalIgnoreCase);
			return Result.Ok<IReadOnlySet<string>>(identifiers);
		}
		catch (Exception parseException)
		{
			return Result.Fail<IReadOnlySet<string>>(
				$"Failed to parse expression '{source}': {parseException.Message}");
		}
	}

	public static Result<LogicalExpression> ParseAndCompile(string source)
	{
		if (string.IsNullOrWhiteSpace(source))
		{
			return Result.Fail<LogicalExpression>("Expression source is empty.");
		}

		try
		{
			var expression = new Expression(source);
			expression.GetParameterNames();
			if (expression.LogicalExpression is null)
			{
				return Result.Fail<LogicalExpression>(
					$"Expression '{source}' produced a null logical expression.");
			}

			return Result.Ok(expression.LogicalExpression);
		}
		catch (Exception parseException)
		{
			return Result.Fail<LogicalExpression>(
				$"Failed to parse expression '{source}': {parseException.Message}");
		}
	}
}
