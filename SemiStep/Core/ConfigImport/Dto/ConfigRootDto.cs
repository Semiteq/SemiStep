namespace Core.ConfigImport.Dto;

public sealed record ConfigRootDto
{
	public required IReadOnlyList<ActionConfigDto> Actions { get; init; }
}
