using System.Collections.Immutable;

namespace SemiStep.Core.Recipes;

public sealed record Step(
	int ActionKey,
	ImmutableDictionary<PropertyId, PropertyValue> Properties)
{
	public Step WithProperty(string key, PropertyValue value)
	{
		return this with { Properties = Properties.SetItem(new PropertyId(key), value) };
	}

	public bool Equals(Step? other)
	{
		if (other is null)
		{
			return false;
		}

		if (ReferenceEquals(this, other))
		{
			return true;
		}

		if (ActionKey != other.ActionKey || Properties.Count != other.Properties.Count)
		{
			return false;
		}

		foreach (var pair in Properties)
		{
			if (!other.Properties.TryGetValue(pair.Key, out var otherValue))
			{
				return false;
			}

			if (!pair.Value.Equals(otherValue))
			{
				return false;
			}
		}

		return true;
	}

	public override int GetHashCode()
	{
		var hash = ActionKey;
		foreach (var pair in Properties)
		{
			hash ^= HashCode.Combine(pair.Key, pair.Value);
		}

		return hash;
	}
}
