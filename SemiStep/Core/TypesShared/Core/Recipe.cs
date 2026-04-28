using System.Collections.Immutable;

namespace TypesShared.Core;

public sealed record Recipe(ImmutableList<Step> Steps)
{
	public static readonly Recipe Empty = new([]);

	public int StepCount => Steps.Count;
}
