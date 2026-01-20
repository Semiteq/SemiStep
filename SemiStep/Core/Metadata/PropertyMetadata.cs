using Core.Entities;

namespace Core.Metadata;

public sealed record PropertyMetadata(
	ColumnId ColumnId,
	PropertyType Type,
	object DefaultValue,
	object? MinValue = null,
	object? MaxValue = null,
	int? MaxLength = null,
	string? Units = null,
	bool IsRequired = true);
