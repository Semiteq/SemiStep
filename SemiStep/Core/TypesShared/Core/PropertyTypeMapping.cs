namespace TypesShared.Core;

public static class PropertyTypeMapping
{
	public static PropertyType FromSystemType(string systemType)
	{
		return systemType.ToLowerInvariant() switch
		{
			"int" or "int32" or "integer" => PropertyType.Int,
			"float" or "single" or "double" => PropertyType.Float,
			_ => PropertyType.String
		};
	}
}
