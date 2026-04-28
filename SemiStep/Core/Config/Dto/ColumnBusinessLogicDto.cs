namespace Config.Dto;

internal sealed class ColumnBusinessLogicDto
{
	public string? PropertyTypeId { get; set; }
	public string? PlcDataType { get; set; }
	public bool ReadOnly { get; set; }
	public bool SaveToCsv { get; set; }
}
