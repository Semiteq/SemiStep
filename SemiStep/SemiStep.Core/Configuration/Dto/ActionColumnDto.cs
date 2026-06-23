namespace SemiStep.Core.Configuration.Dto;

internal sealed class ActionColumnDto
{
	public string? Key { get; set; }
	public string? GroupName { get; set; }
	public string? PropertyTypeId { get; set; }
	public string? DefaultValue { get; set; }
	public Dictionary<int, int>? Targets { get; set; }
}
