using System.Collections.Immutable;

namespace Core.Entities;

public sealed record Step(
	string ActionKey,
	ImmutableDictionary<ColumnId, PropertyValue> Properties);
