using FluentResults;

namespace SemiStep.Core.Recipes.Import.Errors;

public sealed class ActionValueNotIntegerError(string rawAction)
	: Error($"Cannot parse action value '{rawAction}' as integer")
{
	public string RawAction { get; } = rawAction;
}
