namespace SemiStep.Config.Dto;

public sealed class ConfigRootDto
{
	public List<ActionDto>? Actions { get; set; }
	public List<ColumnDto>? Columns { get; set; }
}

public sealed class ActionDto
{
	public string? Key { get; set; }
	public string? DisplayName { get; set; }
	public int InternalId { get; set; }
	public List<string>? PropertyColumns { get; set; }
}

public sealed class ColumnDto
{
	public string? Id { get; set; }
	public string? Type { get; set; }
	public object? DefaultValue { get; set; }
	public object? MinValue { get; set; }
	public object? MaxValue { get; set; }
	public int? MaxLength { get; set; }
	public string? Units { get; set; }
	public bool IsRequired { get; set; } = true;
}
