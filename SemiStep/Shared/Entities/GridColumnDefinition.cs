namespace Shared.Entities;

public sealed record GridColumnDefinition(
	string Key,
	string ColumnType,
	string UiName,
	int Width,
	string PropertyTypeId,
	string PlcDataType,
	bool ReadOnly,
	bool SaveToCsv);
