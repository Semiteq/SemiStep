using Config.Facade;
using Config.Models;

using Shared;

namespace Tests.Config.Helpers;

/// <summary>
/// Helper class for config loading tests.
/// Provides factory methods to load valid and invalid test configurations.
/// </summary>
public static class ConfigTestHelper
{
	/// <summary>
	/// Loads a valid config case and returns the AppConfiguration.
	/// Throws if loading fails.
	/// </summary>
	public static async Task<AppConfiguration> LoadValidCaseAsync(string caseName = "Standard")
	{
		using var tempDir = TestDataCopier.PrepareValidCase(caseName);
		var facade = new ConfigFacade();
		var context = await facade.LoadAsync(tempDir.Path);

		if (context.HasErrors || context.Configuration is null)
		{
			var errors = string.Join("; ", context.Errors.Select(e => e.Message));
			throw new InvalidOperationException($"Expected valid config but got errors: {errors}");
		}

		return context.Configuration;
	}

	/// <summary>
	/// Loads an invalid config case (baseline + overlay) and returns the ConfigContext.
	/// Use this to inspect errors and warnings.
	/// </summary>
	public static async Task<ConfigContext> LoadInvalidCaseAsync(string invalidCaseName)
	{
		using var tempDir = TestDataCopier.PrepareInvalidCase(invalidCaseName);
		var facade = new ConfigFacade();
		return await facade.LoadAsync(tempDir.Path);
	}

	/// <summary>
	/// Loads an invalid config case and verifies that an error with expected content is present.
	/// </summary>
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

	/// <summary>
	/// Loads a config from a temp directory prepared by the test.
	/// </summary>
	public static async Task<ConfigContext> LoadFromTempDirAsync(TempDirectory tempDir)
	{
		var facade = new ConfigFacade();
		return await facade.LoadAsync(tempDir.Path);
	}

	/// <summary>
	/// Loads a standalone config case and returns the ConfigContext.
	/// Use this for self-contained test cases that don't use overlay pattern.
	/// </summary>
	public static async Task<ConfigContext> LoadStandaloneCaseAsync(string caseName)
	{
		using var tempDir = TestDataCopier.PrepareStandaloneCase(caseName);
		var facade = new ConfigFacade();
		return await facade.LoadAsync(tempDir.Path);
	}

	/// <summary>
	/// Creates a ConfigFacade instance for tests that need direct access.
	/// </summary>
	public static ConfigFacade CreateFacade()
	{
		return new ConfigFacade();
	}
}
