using System.Collections.Immutable;

namespace Core.Domain;

public sealed record StepDto
{
	public required string ActionKey { get; init; }
	public required DeployDurationDto DeployDuration { get; init; }
	public required ImmutableDictionary<string, PrimitiveValueDto?> Values { get; init; }
}
