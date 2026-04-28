namespace Config.Dto;

internal sealed class ColumnDto
{
	public string? Key { get; set; }
	public string? ColumnType { get; set; }
	public ColumnUiDto? Ui { get; set; }
	public ColumnBusinessLogicDto? BusinessLogic { get; set; }
}
