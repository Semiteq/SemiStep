using FluentResults;

namespace SemiStep.Core.Recipes.Import.Errors;

public sealed class CsvHeaderMismatchError(string expected, string actual)
	: Error($"CSV header mismatch. Expected: [{expected}], Actual: [{actual}]")
{
	public string Expected { get; } = expected;

	public string Actual { get; } = actual;
}
