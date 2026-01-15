namespace Core.ConfigImport.Dto;

public sealed record ActionConfigDto
{
	public required string Key { get; init; }
	public required short InternalId { get; init; }
	public required IReadOnlyList<PropertyConfigDto> Properties { get; init; }
}
