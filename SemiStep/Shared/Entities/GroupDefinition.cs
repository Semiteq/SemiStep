namespace Shared.Entities;

public sealed record GroupDefinition(
	string GroupId,
	IReadOnlyDictionary<int, string> Items);
