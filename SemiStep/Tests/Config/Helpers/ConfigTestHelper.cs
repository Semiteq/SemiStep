using Config.Facade;
using Config.Models;

using Shared;

namespace Tests.Config.Helpers;

public static class ConfigTestHelper
{
	public static async Task<AppConfiguration> LoadValidCaseAsync(string caseName = "Standard")
	{
		using var tempDir = TestDataCopier.PrepareValidCase(caseName);
		var facade = new ConfigFacade();
		var context = await ConfigFacade.LoadAsync(tempDir.Path);

		if (context.HasErrors || context.Configuration is null)
		{
			var errors = string.Join("; ", context.Errors.Select(e => e.Message));

			throw new InvalidOperationException($"Expected valid config but got errors: {errors}");
		}

		return context.Configuration;
	}

	public static async Task<ConfigContext> LoadInvalidCaseAsync(string invalidCaseName)
	{
		using var tempDir = TestDataCopier.PrepareInvalidCase(invalidCaseName);
		var facade = new ConfigFacade();

		return await ConfigFacade.LoadAsync(tempDir.Path);
	}

	public static async Task LoadExpectingErrorAsync(
		string invalidCaseName,
		string expectedMessageContains)
	{
		var context = await LoadInvalidCaseAsync(invalidCaseName);

		if (!context.HasErrors)
		{
			throw new InvalidOperationException(
				$"Expected errors for case '{invalidCaseName}' but config loaded successfully");
		}

		var matchingError = context.Errors
			.FirstOrDefault(e => e.Message.Contains(expectedMessageContains, StringComparison.OrdinalIgnoreCase));

		if (matchingError is null)
		{
			var actualErrors = string.Join("\n", context.Errors.Select(e => $"  - {e.Message}"));

			throw new InvalidOperationException(
				$"Expected error containing '{expectedMessageContains}' but got:\n{actualErrors}");
		}
	}

	public static async Task<ConfigContext> LoadFromTempDirAsync(TempDirectory tempDir)
	{
		var facade = new ConfigFacade();

		return await ConfigFacade.LoadAsync(tempDir.Path);
	}

	public static async Task<ConfigContext> LoadStandaloneCaseAsync(string caseName)
	{
		using var tempDir = TestDataCopier.PrepareStandaloneCase(caseName);
		var facade = new ConfigFacade();

		return await ConfigFacade.LoadAsync(tempDir.Path);
	}

	public static ConfigFacade CreateFacade()
	{
		return new ConfigFacade();
	}
}
