namespace TypesShared.Core;

public sealed record PropertyValue
{
	private PropertyValue(object value, PropertyType type)
	{
		Value = value;
		Type = type;
	}

	public object Value { get; init; }
	public PropertyType Type { get; init; }

	public static PropertyValue FromInt(int value)
	{
		return new PropertyValue(value, PropertyType.Int);
	}

	public static PropertyValue FromFloat(float value)
	{
		return new PropertyValue(value, PropertyType.Float);
	}

	public static PropertyValue FromString(string value)
	{
		return new PropertyValue(value, PropertyType.String);
	}

	public int AsInt()
	{
		return (int)Value;
	}

	public float AsFloat()
	{
		return (float)Value;
	}

	public string AsString()
	{
		return (string)Value;
	}
}
