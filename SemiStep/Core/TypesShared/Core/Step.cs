using System.Collections.Immutable;

namespace TypesShared.Core;

public sealed record Step(
	int ActionKey,
	ImmutableDictionary<PropertyId, PropertyValue> Properties);
