using Avalonia.Headless.XUnit;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

using Xunit;

namespace SemiStep.Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeCoordinatorLoadRecipeTests
{
	private const string TempFilePrefix = "SemiStep.CoordinatorTest";

	[AvaloniaFact]
	public async Task LoadRecipeAsync_Success_ClearsMessagePanelBeforeAddingNewReasons()
	{
		var (coordinator, panel) = await BuildCoordinatorAsync(services => services.AddCsv());
		coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		var tempFilePath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}.{Guid.NewGuid():N}.csv");
		await coordinator.SaveRecipeAsync(tempFilePath);

		try
		{
			await coordinator.LoadRecipeAsync("nonexistent/path/recipe.csv");

			await coordinator.LoadRecipeAsync(tempFilePath);

			panel.Entries.Should().BeEmpty();
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			File.Delete(tempFilePath);
		}
	}

	[AvaloniaFact]
	public async Task LoadRecipeAsync_Failure_LeavesPanelIntact()
	{
		var (coordinator, panel) = await BuildCoordinatorAsync(services => services.AddCsv());

		try
		{
			panel.AddError("pre-existing error", "Test");

			await coordinator.LoadRecipeAsync("nonexistent/path/recipe.csv");

			panel.Entries.Should().ContainSingle(e => e.Source == "Test");
			panel.Entries.Should().Contain(e => e.IsStructural && e.IsError);
			panel.ErrorCount.Should().BeGreaterThan(0);
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
		}
	}

	[AvaloniaFact]
	public async Task LoadRecipeAsync_Failure_DoesNotEmitSignal()
	{
		var (coordinator, panel) = await BuildCoordinatorAsync(services => services.AddCsv());

		try
		{
			var sink = new RecordingRecipeSink();
			coordinator.Mutated += sink.OnMutation;

			await coordinator.LoadRecipeAsync("nonexistent/path/recipe.csv");

			sink.Signals.Should().BeEmpty();
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
		}
	}

	[AvaloniaFact]
	public async Task LoadRecipeAsync_EmptyRecipe_PanelHasNoWarnings()
	{
		var (coordinator, panel) = await BuildCoordinatorAsync(services => services.AddCsv());
		var tempFilePath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}.{Guid.NewGuid():N}.csv");

		try
		{
			// Save the default empty recipe so we have a valid CSV file with no steps.
			await coordinator.SaveRecipeAsync(tempFilePath);

			var result = await coordinator.LoadRecipeAsync(tempFilePath);

			result.IsSuccess.Should().BeTrue("loading a valid empty CSV must succeed");
			panel.Entries.Should().NotContain(e => e.IsWarning,
				"an empty recipe is a normal initial state and must not produce warnings");
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			File.Delete(tempFilePath);
		}
	}

	[AvaloniaFact]
	public async Task SaveRecipeAsync_Failure_ReturnsFailed()
	{
		var (coordinator, panel) = await BuildCoordinatorAsync(services =>
		{
			services.AddCsv();
			services.AddSingleton<CsvService>(sp => new ThrowingCsvService(
				sp.GetRequiredService<CsvFileSerializer>()));
		});

		try
		{
			var result = await coordinator.SaveRecipeAsync("any/path.csv");

			result.IsFailed.Should().BeTrue();
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
		}
	}

	[AvaloniaFact]
	public async Task SaveRecipeAsync_IoException_ConvertsToFailedResult()
	{
		var (coordinator, panel) = await BuildCoordinatorAsync(services => services.AddCsv());

		// Use an existing file as the directory portion of the target path.
		// File.Move into a "directory" that is actually a file throws an IOException
		// from inside CsvFileIo.WriteRecipeFileAsync — exercises the catch block in CsvService.SaveAsync.
		var blockingFile = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}.{Guid.NewGuid():N}.blocker");
		await File.WriteAllTextAsync(blockingFile, "blocker");
		var unwritablePath = Path.Combine(blockingFile, "child.csv");

		try
		{
			var result = await coordinator.SaveRecipeAsync(unwritablePath);

			result.IsFailed.Should().BeTrue(
				"a real IOException from the underlying file system must be converted to "
				+ "Result.Fail by CsvService.SaveAsync");
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			File.Delete(blockingFile);
		}
	}

	private static async Task<(RecipeCoordinator Coordinator, MessagePanelViewModel Panel)>
		BuildCoordinatorAsync(Action<IServiceCollection> registerCsvService)
	{
		var configDir = TestConfigLocator.GetConfigDirectory("WithGroups");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);
		var configuration = configLoadResult.EnsureSuccess("Test config load");

		var serviceCollection = new ServiceCollection()
			.AddLogging()
			.AddSingleton(configuration)
			.AddRecipe()
			.AddClipboard()
			.AddSingleton<StubS7Service>()
			.AddSingleton<IS7Connection>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7Reader>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7ExecutionStream>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IPlcSyncService, StubPlcSyncService>();

		registerCsvService(serviceCollection);

		var services = serviceCollection.BuildServiceProvider();

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

		return (coordinator, panel);
	}
}
