using FluentResults;

using SemiStep.Core.Recipes;

namespace SemiStep.Core.Configuration.Validation;

internal static class ActionTreeValidator
{
	public static Result Validate(IReadOnlyCollection<ActionDefinition> actions)
	{
		var resolveResult = ActionTreeResolver.Resolve(actions);

		return resolveResult.IsFailed
			? resolveResult.ToResult()
			: resolveResult.Value.Count == 0
			? Result.Fail(new NoPrimaryActionsError())
			: Result.Ok();
	}
}
