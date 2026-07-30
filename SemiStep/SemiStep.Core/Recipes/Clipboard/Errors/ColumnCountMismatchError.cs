using FluentResults;

namespace SemiStep.Core.Recipes.Clipboard.Errors;

public sealed class ColumnCountMismatchError(int rowNumber, int expected, int actual)
	: Error($"Column count mismatch on row {rowNumber}: expected {expected}, got {actual}. "
		+ "The clipboard data does not match the current configuration.")
{
	public int RowNumber { get; } = rowNumber;

	public int Expected { get; } = expected;

	public int Actual { get; } = actual;
}
