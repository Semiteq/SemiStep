namespace SemiStep.Core.Configuration.Dto;

internal sealed class ActionDto
{
	public int Id { get; set; }
	public string? UiName { get; set; }
	public string? Role { get; set; }
	public string? DeployDuration { get; set; }
	public List<ActionColumnDto>? Columns { get; set; }

	public FormulaDto? Formula { get; set; }
}
