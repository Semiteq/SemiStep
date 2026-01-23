namespace Config.Dto;

public sealed class ColumnDto
{
	public string? Key { get; set; }
	public string? ColumnType { get; set; }
	public ColumnUiDto? Ui { get; set; }
	public ColumnBusinessLogicDto? BusinessLogic { get; set; }
}

public sealed class ColumnUiDto
{
	public string? UiName { get; set; }
	public int Width { get; set; }
}

public sealed class ColumnBusinessLogicDto
{
	public string? PropertyTypeId { get; set; }
	public string? PlcDataType { get; set; }
	public bool ReadOnly { get; set; }
	public bool SaveToCsv { get; set; }
}
