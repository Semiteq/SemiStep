namespace Core.ConfigImport.Dto;

public sealed record PropertyConfigDto
{
	public required string ColumnId { get; init; }
	public required string PropertyTypeId { get; init; }
}
