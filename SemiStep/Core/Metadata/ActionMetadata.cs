using Core.Entities;

namespace Core.Metadata;

public sealed record ActionMetadata(
	string Key,
	string DisplayName,
	int InternalId,
	IReadOnlyList<ColumnId> PropertyColumns);
