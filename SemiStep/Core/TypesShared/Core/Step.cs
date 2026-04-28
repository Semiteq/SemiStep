using System.Collections.Immutable;

namespace TypesShared.Core;

public sealed record Step(
	int ActionKey,
	ImmutableDictionary<PropertyId, PropertyValue> Properties)
{
	public Step WithProperty(string key, PropertyValue value)
	{
		return this with { Properties = Properties.SetItem(new PropertyId(key), value) };
	}
}
