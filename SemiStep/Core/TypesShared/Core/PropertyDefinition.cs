namespace TypesShared.Core;

public sealed record PropertyTypeDefinition(
	string Id,
	string SystemType,
	string FormatKind,
	string? Units,
	double? Min,
	double? Max,
	int? MaxLength);
