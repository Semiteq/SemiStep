using System.Collections.Immutable;

namespace Core.Entities;

/// <summary>
/// Represents an immutable snapshot of an entire recipe.
/// </summary>
/// <param name="Steps">The immutable list of steps that make up the recipe.</param>
public record Recipe(IImmutableList<Step> Steps)
{
	public static Recipe Empty => new(ImmutableList<Step>.Empty);
}
