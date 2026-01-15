using System.Collections.Immutable;

namespace Core.Entities;

internal record Recipe(IImmutableList<Step> Steps)
{
	internal static Recipe Empty => new(ImmutableList<Step>.Empty);
}
