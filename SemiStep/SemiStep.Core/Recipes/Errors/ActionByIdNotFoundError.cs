using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class ActionByIdNotFoundError(int id)
	: Error($"Action with id {id} not found")
{
	public int Id { get; } = id;
}
