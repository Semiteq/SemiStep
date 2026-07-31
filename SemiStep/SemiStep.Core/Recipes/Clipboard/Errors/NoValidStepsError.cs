using FluentResults;

namespace SemiStep.Core.Recipes.Clipboard.Errors;

public sealed class NoValidStepsError()
	: Error("No valid steps found in clipboard data")
{
}
