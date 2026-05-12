namespace SemiStep.Core.Recipes;

public sealed record GridColumnDefinition(
	string Key,
	string ColumnType,
	string UiName,
	string PropertyTypeId,
	bool ReadOnly,
	bool SaveToCsv);
