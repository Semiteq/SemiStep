namespace SemiStep.Core.Recipes;

public sealed record PropertyTypeDefinition(
	string Id,
	string SystemType,
	string FormatKind,
	string? Units,
	double? Min,
	double? Max,
	int? MaxLength);

public static class SystemTypes
{
	public const string Int = "int";
	public const string Float = "float";

	public static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
}
