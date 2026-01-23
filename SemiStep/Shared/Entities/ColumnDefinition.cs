namespace Shared.Entities;

public sealed record ColumnDefinition(
	string Key,
	string ColumnType,
	string UiName,
	int Width,
	string PropertyTypeId,
	string PlcDataType,
	bool ReadOnly,
	bool SaveToCsv);
