namespace Recipe.Entities;

public sealed record PropertyValue(object Value, PropertyType Type)
{
	public int AsInt() => (int)Value;

	public float AsFloat() => (float)Value;

	public string AsString() => (string)Value;
}
