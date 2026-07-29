using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class AtColumnError(string columnKey, IError inner)
	: Error($"Column '{columnKey}': {inner.Message}")
{
	public string ColumnKey { get; } = columnKey;

	public IError Inner { get; } = inner;
}
