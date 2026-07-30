using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class AtRowError(int rowNumber, IError inner)
	: Error($"Row {rowNumber}: {inner.Message}")
{
	public int RowNumber { get; } = rowNumber;

	public IError Inner { get; } = inner;
}
