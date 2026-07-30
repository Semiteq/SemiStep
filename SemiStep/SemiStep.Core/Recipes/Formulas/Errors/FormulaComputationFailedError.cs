using FluentResults;

namespace SemiStep.Core.Recipes.Formulas.Errors;

public sealed class FormulaComputationFailedError(string target, IError inner)
	: Error($"Formula computation for target '{target}' failed: {inner.Message}")
{
	public string Target { get; } = target;

	public IError Inner { get; } = inner;
}
