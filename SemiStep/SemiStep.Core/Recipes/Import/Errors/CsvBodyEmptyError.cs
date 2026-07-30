using FluentResults;

namespace SemiStep.Core.Recipes.Import.Errors;

public sealed class CsvBodyEmptyError()
	: Error("CSV body is empty")
{
}
