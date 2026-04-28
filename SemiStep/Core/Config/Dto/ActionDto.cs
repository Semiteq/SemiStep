namespace Config.Dto;

internal sealed class ActionDto
{
	public short Id { get; set; }
	public string? UiName { get; set; }
	public string? DeployDuration { get; set; }
	public List<ActionColumnDto>? Columns { get; set; }
}
