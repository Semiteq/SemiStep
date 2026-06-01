using System.Collections.Immutable;

using Avalonia.Headless.XUnit;

using FluentAssertions;

using FluentResults;

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
			// Establish a dirty panel precondition via a genuine structural warning: an
			// unclosed For loop is a real snapshot reason, so the snapshot-fed panel becomes non-empty.
			coordinator.AppendStep(RecipeTestDriver.ForLoopActionId);
			panel.Entries.Should().Contain(
				entry => entry.IsWarning,
				"an unclosed For loop is a genuine structural defect that lands in the snapshot");

			await coordinator.LoadRecipeAsync(tempFilePath);

			panel.Entries.Should().BeEmpty(
				"a successful load rebuilds the panel from the loaded recipe's snapshot, clearing the prior warning");
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			File.Delete(tempFilePath);
		}
	}

	[AvaloniaFact]
	public async Task LoadRecipeAsync_Failure_DoesNotPopulateValidationPanel()
	{
		var (coordinator, panel) = await BuildCoordinatorAsync(services => services.AddCsv());

		try
		{
			panel.Entries.Should().BeEmpty("the panel starts clean before any load");

			var result = await coordinator.LoadRecipeAsync("nonexistent/path/recipe.csv");

			result.IsFailed.Should().BeTrue("loading a nonexistent file must fail");
			result.Errors.Should().NotBeEmpty(
				"a failed load is an operation outcome and must carry its error on the returned Result");

			panel.HasErrors.Should().BeFalse(
				"a failed load is an operation outcome, not a structural defect, so it leaves the snapshot-fed panel untouched");
			panel.ErrorCount.Should().Be(0);
			panel.Entries.Should().BeEmpty(
				"the panel reflects the unchanged recipe snapshot; the failure is surfaced transiently by the initiating VM");
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
		}
	}

	[AvaloniaFact]
	public async Task LoadRecipeAsync_AnalyzerFailure_DoesNotPopulateValidationPanel()
	{
		var (coordinator, panel, services) = await BuildCoordinatorWithServicesAsync(services => services.AddCsv());

		var csvService = services.GetRequiredService<CsvService>();
		var tempFilePath = Path.Combine(Path.GetTempPath(), $"{TempFilePrefix}.{Guid.NewGuid():N}.csv");

		try
		{
			// A recipe whose loop nesting exceeds RecipeAnalyzer.MaxLoopDepth (3) passes
			// ImportedRecipeValidator (which only checks per-step property validity, not loop
			// structure) but FAILS RecipeAnalyzer.Analyze. Such a load stores a FAILED snapshot
			// as the current one, whose IError reasons must NOT leak into the validation panel.
			var deeplyNestedRecipe = BuildRecipeWithLoopNesting(4);
			(await csvService.SaveAsync(deeplyNestedRecipe, tempFilePath))
				.EnsureSuccess("Writing the deeply-nested CSV fixture");

			panel.Entries.Should().BeEmpty("the panel starts clean before any load");

			var result = await coordinator.LoadRecipeAsync(tempFilePath);

			result.IsFailed.Should().BeTrue(
				"a recipe whose loop nesting exceeds the analyzer limit must fail to load");
			result.Errors.Should().NotBeEmpty(
				"the analyzer failure is an operation outcome carried on the returned Result");

			panel.HasErrors.Should().BeFalse(
				"an analyzer failure on load is an operation outcome, not the current recipe's structural state, "
				+ "so it must not leak into the snapshot-fed validation panel");
			panel.ErrorCount.Should().Be(0);
			panel.Entries.Should().BeEmpty(
				"the validation panel surfaces reasons only from a successful snapshot");
		}
		finally
		{
			coordinator.Dispose();
			panel.Dispose();
			services.Dispose();
			File.Delete(tempFilePath);
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
		var (coordinator, panel, _) = await BuildCoordinatorWithServicesAsync(registerCsvService);
		return (coordinator, panel);
	}

	private static async Task<(RecipeCoordinator Coordinator, MessagePanelViewModel Panel, ServiceProvider Services)>
		BuildCoordinatorWithServicesAsync(Action<IServiceCollection> registerCsvService)
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
			.AddSingleton<IPlcSyncService, StubPlcSyncService>()
			.AddSingleton<IPlcSyncOwnership, StubPlcSyncOwnership>();

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

		return (coordinator, panel, services);
	}

	private static Recipe BuildRecipeWithLoopNesting(int depth)
	{
		var forStep = new Step(
			RecipeTestDriver.ForLoopActionId,
			new Dictionary<PropertyId, PropertyValue>
			{
				[new PropertyId(RecipeTestDriver.TaskColumn)] = PropertyValue.FromFloat(1f)
			}.ToImmutableDictionary());
		var endForStep = new Step(
			RecipeTestDriver.EndForLoopActionId,
			ImmutableDictionary<PropertyId, PropertyValue>.Empty);

		var steps = ImmutableList<Step>.Empty;
		for (var i = 0; i < depth; i++)
		{
			steps = steps.Add(forStep);
		}

		for (var i = 0; i < depth; i++)
		{
			steps = steps.Add(endForStep);
		}

		return new Recipe(steps);
	}
}
