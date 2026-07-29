using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class ActionByNameNotFoundError(string name)
	: Error($"Action with name '{name}' not found")
{
	public string Name { get; } = name;
}
