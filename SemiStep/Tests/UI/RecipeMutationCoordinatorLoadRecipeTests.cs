using Avalonia.Threading;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;

using Tests.Core.Helpers;
using Tests.Helpers;

using UI.Coordinator;
using UI.MessageService;

using Xunit;

namespace Tests.UI;

[Trait("Component", "UI")]
[Trait("Area", "Coordinator")]
[Trait("Category", "Integration")]
public sealed class RecipeMutationCoordinatorLoadRecipeTests
{
	private const string TempFilePrefix = "SemiStep.CoordinatorTest";

	[Fact]
	public async Task LoadRecipeAsync_Success_ClearsMessagePanelBeforeAddingNewReasons()
	{
		var (coordinator, panel, tempFilePath) = await BuildCoordinatorWithCsvAndSavedRecipeAsync();

		try
		{
			await coordinator.LoadRecipeAsync("nonexistent/path/recipe.csv");
			Dispatcher.UIThread.RunJobs(null);

			await coordinator.LoadRecipeAsync(tempFilePath);
			Dispatcher.UIThread.RunJobs(null);

			panel.Entries.Should().BeEmpty();
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			File.Delete(tempFilePath);
		}
	}

	[Fact]
	public async Task LoadRecipeAsync_Failure_LeavesPanelIntact()
	{
		var (coordinator, panel) = await BuildCoordinatorWithCsvAsync();

		try
		{
			panel.AddError("pre-existing error", "Test");
			Dispatcher.UIThread.RunJobs(null);

			await coordinator.LoadRecipeAsync("nonexistent/path/recipe.csv");
			Dispatcher.UIThread.RunJobs(null);

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

	[Fact]
	public async Task LoadRecipeAsync_Failure_DoesNotEmitSignal()
	{
		var (coordinator, panel) = await BuildCoordinatorWithCsvAsync();

		try
		{
			var signals = new List<MutationSignal>();
			using var sub = coordinator.StateChanged.Subscribe(signals.Add);

			await coordinator.LoadRecipeAsync("nonexistent/path/recipe.csv");

			signals.Should().BeEmpty();
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
		}
	}

	[Fact]
	public async Task LoadRecipeAsync_Success_WithWarnings_ShowsWarningsInPanel()
	{
		var (coordinator, panel) = await BuildCoordinatorWithCsvAsync();
		var tempFilePath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}.{Guid.NewGuid():N}.csv");

		try
		{
			// Save the default empty recipe so we have a valid CSV file with no steps.
			await coordinator.SaveRecipeAsync(tempFilePath);

			// Load it back — an empty recipe triggers a "Recipe has no steps" warning from the analyzer.
			var result = await coordinator.LoadRecipeAsync(tempFilePath);
			Dispatcher.UIThread.RunJobs(null);

			result.IsSuccess.Should().BeTrue("loading a valid CSV should succeed even when it has warnings");
			panel.Entries.Should().Contain(e => e.IsWarning,
				"the message panel must show the 'Recipe has no steps' warning after loading an empty recipe");
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			File.Delete(tempFilePath);
		}
	}

	private static async Task<(RecipeMutationCoordinator Coordinator, MessagePanelViewModel Panel)>
		BuildCoordinatorWithCsvAsync()
	{
		return await BuildCoordinatorAsync(services => services.AddCsv());
	}

	private static async Task<(
		RecipeMutationCoordinator Coordinator,
		MessagePanelViewModel Panel,
		string TempFilePath)> BuildCoordinatorWithCsvAndSavedRecipeAsync()
	{
		var (coordinator, panel) = await BuildCoordinatorWithCsvAsync();

		coordinator.AppendStep(RecipeTestDriver.WaitActionId);

		var tempFilePath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}.{Guid.NewGuid():N}.csv");
		await coordinator.SaveRecipeAsync(tempFilePath);

		return (coordinator, panel, tempFilePath);
	}

	[Fact]
	public async Task SaveRecipeAsync_Failure_ReturnsFailed()
	{
		var (coordinator, panel) = await BuildCoordinatorWithThrowingCsvAsync();

		try
		{
			var result = await coordinator.SaveRecipeAsync("any/path.csv");
			Dispatcher.UIThread.RunJobs(null);

			result.IsFailed.Should().BeTrue();
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
		}
	}

	[Fact]
	public async Task SaveRecipeAsync_IoException_ConvertsToFailedResult()
	{
		var (coordinator, panel) = await BuildCoordinatorWithCsvAsync();

		// Use an existing file as the directory portion of the target path.
		// File.Move into a "directory" that is actually a file throws an IOException
		// from inside CsvFileIo.WriteRecipeFileAsync — exercises the catch block in CsvService.SaveAsync.
		var blockingFile = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}.{Guid.NewGuid():N}.blocker");
		await File.WriteAllTextAsync(blockingFile, "blocker");
		var unwritablePath = Path.Combine(blockingFile, "child.csv");

		try
		{
			var result = await coordinator.SaveRecipeAsync(unwritablePath);
			Dispatcher.UIThread.RunJobs(null);

			result.IsFailed.Should().BeTrue(
				"a real IOException from the underlying file system must be converted to Result.Fail by CsvService.SaveAsync");
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			File.Delete(blockingFile);
		}
	}

	private static async Task<(RecipeMutationCoordinator Coordinator, MessagePanelViewModel Panel)>
		BuildCoordinatorWithThrowingCsvAsync()
	{
		return await BuildCoordinatorAsync(services =>
		{
			services.AddCsv();
			services.AddSingleton<CsvService>(sp => new ThrowingCsvService(
				sp.GetRequiredService<CsvFileSerializer>()));
		});
	}

	private static async Task<(RecipeMutationCoordinator Coordinator, MessagePanelViewModel Panel)>
		BuildCoordinatorAsync(Action<IServiceCollection> registerCsvService)
	{
		var configDir = TestConfigLocator.GetConfigDirectory("WithGroups");
		var configLoadResult = await ConfigFacade.LoadAndValidateAsync(configDir);

		var serviceCollection = new ServiceCollection()
			.AddSingleton(configLoadResult.Value)
			.AddRecipe()
			.AddClipboard()
			.AddSingleton<StubS7Service>()
			.AddSingleton<IS7Connection>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7Reader>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IS7ExecutionStream>(sp => sp.GetRequiredService<StubS7Service>())
			.AddSingleton<IPlcSyncService, StubPlcSyncService>();

		registerCsvService(serviceCollection);

		var services = serviceCollection.BuildServiceProvider();

		var workspace = services.GetRequiredService<RecipeWorkspace>();
		var editor = services.GetRequiredService<RecipeEditor>();
		var plc = services.GetRequiredService<PlcLifecycleManager>();
		plc.Initialize();
		workspace.Reset();

		var configRegistry = services.GetRequiredService<ConfigRegistry>();
		var panel = new MessagePanelViewModel();
		var clipboardSerializer = services.GetRequiredService<ClipboardSerializer>();
		var importedRecipeValidator = services.GetRequiredService<ImportedRecipeValidator>();
		var queryService = new RecipeQueryService(workspace, plc, clipboardSerializer, importedRecipeValidator, configRegistry);
		var appConfiguration = services.GetRequiredService<AppConfiguration>();
		var csvService = services.GetRequiredService<CsvService>();
		var coordinator = new RecipeMutationCoordinator(
			workspace,
			editor,
			plc,
			csvService,
			importedRecipeValidator,
			appConfiguration,
			queryService,
			panel);
		coordinator.Initialize();

		return (coordinator, panel);
	}
}
