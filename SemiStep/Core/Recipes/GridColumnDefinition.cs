namespace SemiStep.Core.Recipes;

public sealed record GridColumnDefinition(
	string Key,
	string ColumnType,
	string UiName,
	string PropertyTypeId,
	string PlcDataType,
	bool ReadOnly,
	bool SaveToCsv);
