namespace SemiStep.Core.Recipes;

public sealed record GroupDefinition(
	string GroupId,
	IReadOnlyDictionary<int, string> Items);
