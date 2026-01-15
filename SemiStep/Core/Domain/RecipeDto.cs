using System.Collections.Immutable;

namespace Core.Domain;

public sealed record RecipeDto
{
	public required ImmutableList<StepDto> Steps { get; init; }
}
