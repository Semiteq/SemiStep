namespace SemiStep.Core.Recipes;

public sealed record ActionPropertyDefinition(
	string Key,
	string? GroupName,
	string PropertyTypeId,
	string? DefaultValue);
