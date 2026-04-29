using FluentResults;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Import;

namespace Tests.Helpers;

internal sealed class ThrowingCsvService(CsvFileSerializer csvFileSerializer)
	: CsvService(csvFileSerializer, NullLogger<CsvService>.Instance)
{
	public override Task<Result<Recipe>> LoadAsync(string filePath)
	{
		return Task.FromResult(Result.Fail<Recipe>("ThrowingCsvService does not support loading."));
	}

	public override Task<Result> SaveAsync(Recipe recipe, string filePath)
	{
		return Task.FromResult(Result.Fail("Simulated disk write failure."));
	}
}
