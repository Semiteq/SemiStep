using FluentResults;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Facade;

namespace SemiStep.Tests.Config.Helpers;

internal static class ConfigTestHelper
{
	public static async Task<AppConfiguration> LoadValidCaseAsync(string caseName = "Standard")
	{
		using var tempDir = TestDataCopier.PrepareValidCase(caseName);
		var result = await ConfigFacade.LoadAndValidateAsync(tempDir.Path);

		if (result.IsFailed)
		{
			var errors = string.Join("; ", result.Errors.Select(e => e.Message));

			throw new InvalidOperationException($"Expected valid config but got errors: {errors}");
		}

		return result.Value;
	}

	public static async Task<Result<AppConfiguration>> LoadInvalidCaseAsync(string invalidCaseName)
	{
		using var tempDir = TestDataCopier.PrepareInvalidCase(invalidCaseName);

		return await ConfigFacade.LoadAndValidateAsync(tempDir.Path);
	}

	public static async Task<Result<AppConfiguration>> LoadStandaloneCaseAsync(string caseName)
	{
		using var tempDir = TestDataCopier.PrepareStandaloneCase(caseName);

		return await ConfigFacade.LoadAndValidateAsync(tempDir.Path);
	}
}
