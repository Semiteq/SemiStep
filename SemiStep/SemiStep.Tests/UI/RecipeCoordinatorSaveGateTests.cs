using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeCoordinatorSaveGateTests
{
	private const string TempFilePrefix = "SemiStep.CoordinatorSaveGate";

	[AvaloniaFact]
	public async Task Save_OnDefectiveRecipe_FailsAndWritesNoFile()
	{
		var (coordinator, panel, session) = await BuildCoordinatorAsync();
		var tempFilePath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}.{Guid.NewGuid():N}.csv");

		try
		{
			var driver = new RecipeTestDriver(session);
			driver.AddFor(3).AddWait(1f);

			session.IsValid.Should().BeFalse("the recipe has an unclosed For loop");

			var result = await coordinator.SaveRecipeAsync(tempFilePath);

			result.IsFailed.Should().BeTrue("Save must reject a recipe with structural defects");
			File.Exists(tempFilePath).Should().BeFalse("the file must not be written when Save is rejected");
			panel.Entries.Should().Contain(
				e => e.IsWarning && e.Message.Contains("Unclosed For loop", StringComparison.OrdinalIgnoreCase),
				"the underlying analyzer warning must still surface in the message panel after the rejected Save");
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			if (File.Exists(tempFilePath))
			{
				File.Delete(tempFilePath);
			}
		}
	}

	private static async Task<(RecipeCoordinator Coordinator, MessagePanelViewModel Panel, RecipeSession Session)>
		BuildCoordinatorAsync()
	{
		var configDir = TestConfigLocator.GetConfigDirectory("WithGroups");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);
		var configuration = configLoadResult.EnsureSuccess("Test config load");

		var services = new ServiceCollection()
			.AddLogging()
			.AddSingleton(configuration)
			.AddRecipe()
			.AddClipboard()
			.AddCsv()
			.AddSingleton<StubS7Service>()
			.AddSingleton<IS7Connection>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7Reader>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7ExecutionStream>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IPlcSyncService, StubPlcSyncService>()
			.AddSingleton<IPlcSyncOwnership, StubPlcSyncOwnership>()
			.BuildServiceProvider();

		var session = services.GetRequiredService<RecipeSession>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		session.Reset().EnsureSuccess("Session reset");

		var recipeMetadataRegistry = services.GetRequiredService<RecipeMetadataRegistry>();
		var panel = new MessagePanelViewModel();
		var importedRecipeValidator = services.GetRequiredService<ImportedRecipeValidator>();
		var appConfiguration = services.GetRequiredService<AppConfiguration>();
		var csvService = services.GetRequiredService<CsvService>();
		var coordinator = new RecipeCoordinator(
			session,
			plc,
			csvService,
			importedRecipeValidator,
			appConfiguration,
			recipeMetadataRegistry,
			panel,
			NullLogger<RecipeCoordinator>.Instance);
		coordinator.Initialize();

		return (coordinator, panel, session);
	}
}
