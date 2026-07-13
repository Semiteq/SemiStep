namespace SemiStep.Core.Configuration.Dto;

internal static class GridOrientationValues
{
	public const string RowsAsSteps = "rows_as_steps";
	public const string ColumnsAsSteps = "columns_as_steps";

	// GridStyleValidator rejects unknown values before this parser runs; throwing here is
	// defense-in-depth against a future path that maps an unvalidated DTO.
	public static GridOrientation Parse(string? value)
	{
		return value switch
		{
			null or RowsAsSteps => GridOrientation.RowsAsSteps,
			ColumnsAsSteps => GridOrientation.ColumnsAsSteps,
			_ => throw new ArgumentException($"Unknown grid orientation value '{value}'.", nameof(value)),
		};
	}

	public static string Serialize(GridOrientation orientation)
	{
		return orientation == GridOrientation.ColumnsAsSteps ? ColumnsAsSteps : RowsAsSteps;
	}
}
