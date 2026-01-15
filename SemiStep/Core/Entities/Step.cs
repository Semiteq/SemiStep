using System.Collections.Immutable;

using Core.Properties;

namespace Core.Entities;

internal sealed record Step(
	ImmutableDictionary<ColumnId, Property?> Properties,
	DeployDuration DeployDuration);
