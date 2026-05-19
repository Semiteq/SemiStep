namespace SemiStep.Core.Configuration.Dto;

internal sealed class FormulaDto
{
	public List<string>? RecalcOrder { get; set; }

	public Dictionary<string, string>? Expressions { get; set; }
}
