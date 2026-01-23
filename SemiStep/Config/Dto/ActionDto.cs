namespace Config.Dto;

public sealed class ActionDto
{
	public short Id { get; set; }
	public string? UiName { get; set; }
	public string? DeployDuration { get; set; }
	public List<ActionColumnDto>? Columns { get; set; }
}

public sealed class ActionColumnDto
{
	public string? Key { get; set; }
	public string? GroupName { get; set; }
	public string? PropertyTypeId { get; set; }
	public string? DefaultValue { get; set; }
}
