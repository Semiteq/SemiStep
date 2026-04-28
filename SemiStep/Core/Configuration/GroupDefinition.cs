namespace SemiStep.Core.Configuration;

public sealed record GroupDefinition(
	string GroupId,
	IReadOnlyDictionary<int, string> Items);
