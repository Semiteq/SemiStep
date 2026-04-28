namespace TypesShared.Core;

public sealed record ActionPropertyDefinition(
	string Key,
	string? GroupName,
	string PropertyTypeId,
	string? DefaultValue);
