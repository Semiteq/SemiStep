namespace Config.Dto;

internal sealed class PropertyDto
{
	public string? PropertyTypeId { get; set; }
	public string? SystemType { get; set; }
	public string? FormatKind { get; set; }
	public string? Units { get; set; }
	public double? Min { get; set; }
	public double? Max { get; set; }
	public int? MaxLength { get; set; }
}
