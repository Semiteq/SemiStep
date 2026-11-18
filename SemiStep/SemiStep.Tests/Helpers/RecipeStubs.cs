using System.Collections.Immutable;

using SemiStep.Core.Recipes;

namespace SemiStep.Tests.Helpers;

/// <summary>Recipe builders for tests that need hand-built recipes.</summary>
public static class RecipeStubs
{
	/// <summary>A recipe of <paramref name="stepCount"/> property-less steps of the given action.</summary>
	public static Recipe WithSteps(int actionKey, int stepCount)
	{
		var steps = Enumerable
			.Range(0, stepCount)
			.Select(_ => new Step(actionKey, []));

		return new Recipe([.. steps]);
	}

	/// <summary>
	/// A copy carrying the same content in fresh instances, so an equality check compares content
	/// rather than references.
	/// </summary>
	public static Recipe DeepCopy(Recipe recipe)
	{
		var copiedSteps = recipe.Steps
			.Select(step => new Step(step.ActionKey, ImmutableDictionary.CreateRange(step.Properties)));

		return new Recipe([.. copiedSteps]);
	}
}
