namespace Recipe.Entities;

public readonly record struct ColumnId(string Value)
{
	public override string ToString() => Value;
}
