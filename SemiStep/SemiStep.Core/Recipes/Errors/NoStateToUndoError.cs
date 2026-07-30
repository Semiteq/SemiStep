using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class NoStateToUndoError()
	: Error("No state to undo to")
{
}
