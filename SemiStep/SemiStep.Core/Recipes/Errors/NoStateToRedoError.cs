using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class NoStateToRedoError()
	: Error("No state to redo to")
{
}
