using System.Collections.Immutable;

namespace Recipe.Entities;

public sealed record Step(
	string ActionKey,
	ImmutableDictionary<ColumnId, PropertyValue> Properties);
