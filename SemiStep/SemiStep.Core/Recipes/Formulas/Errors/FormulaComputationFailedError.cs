using FluentResults;

namespace SemiStep.Core.Recipes.Formulas.Errors;

public sealed class FormulaComputationFailedError : Error
{
	public FormulaComputationFailedError(string target, string reason)
		: base($"Formula computation for target '{target}' failed: {reason}")
	{
		Target = target;
		Reason = reason;
		Metadata["target"] = target;
		Metadata["reason"] = reason;
	}

	public string Target { get; }

	public string Reason { get; }
}
