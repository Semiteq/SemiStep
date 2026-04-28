namespace TypesShared.Core;

public readonly record struct PropertyId(string Value)
{
	public override string ToString()
	{
		return Value;
	}
}
