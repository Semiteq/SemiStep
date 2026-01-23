namespace Shared.Entities;

public sealed record ActionColumnDefinition(
	string Key,
	string? GroupName,
	string PropertyTypeId,
	string? DefaultValue);
