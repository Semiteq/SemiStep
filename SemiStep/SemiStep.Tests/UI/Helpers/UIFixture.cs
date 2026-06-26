using System.Reactive.Concurrency;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using SemiStep.UI.Clipboard;
using SemiStep.UI.Coordinator;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI.Helpers;

public sealed class UIFixture : IAsyncLifetime
{
	public RecipeSession Session { get; private set; } = null!;
	public PlcLifecycleManager Plc { get; private set; } = null!;
	public StubPlcSyncService PlcSyncService { get; private set; } = null!;
	public StubS7Service S7Service { get; private set; } = null!;
	public RecipeMetadataRegistry RecipeMetadataRegistry { get; private set; } = null!;
	public AppConfiguration AppConfiguration { get; private set; } = null!;
	public MessagePanelViewModel MessagePanel { get; private set; } = null!;
	public RecipeCoordinator Coordinator { get; private set; } = null!;
	public StubS7Service StubS7 { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		var (services, session, plc) = await CoreTestHelper.BuildAsync("WithGroups");
		Session = session;
		Plc = plc;
		PlcSyncService = (StubPlcSyncService)services.GetRequiredService<IPlcSyncService>();
		S7Service = services.GetRequiredService<StubS7Service>();
		RecipeMetadataRegistry = services.GetRequiredService<RecipeMetadataRegistry>();
		AppConfiguration = services.GetRequiredService<AppConfiguration>();
		MessagePanel = new MessagePanelViewModel();
		StubS7 = services.GetRequiredService<StubS7Service>();
		var importedRecipeValidator = services.GetRequiredService<ImportedRecipeValidator>();
		var csvService = services.GetRequiredService<CsvService>();
		Coordinator = new RecipeCoordinator(
			Session,
			Plc,
			csvService,
			importedRecipeValidator,
			AppConfiguration,
			RecipeMetadataRegistry,
			MessagePanel,
			NullLogger<RecipeCoordinator>.Instance);
		Coordinator.Initialize();
	}

	public ValueTask DisposeAsync()
	{
		Coordinator.Dispose();
		MessagePanel.Dispose();
		return ValueTask.CompletedTask;
	}

	public MainWindowViewModel CreateMainWindowViewModel(
		Func<GridStyleEditorViewModel>? styleEditorFactory = null)
	{
		var grid = new RecipeGridViewModel(
			Coordinator,
			RecipeMetadataRegistry,
			MessagePanel,
			NullLogger<RecipeGridViewModel>.Instance);

		var commands = new RecipeCommandsViewModel(Coordinator, grid);

		var clipboardSerializer = new ClipboardSerializer(RecipeMetadataRegistry);
		var importedRecipeValidator = new ImportedRecipeValidator(RecipeMetadataRegistry);
		var clipboard = new ClipboardViewModel(
			Coordinator,
			grid,
			clipboardSerializer,
			importedRecipeValidator,
			MessagePanel);

		var recipeFile = new RecipeFileViewModel(Coordinator, MessagePanel);

		var columnBuilder = new ColumnBuilder(AppConfiguration.GridStyle, RecipeMetadataRegistry);

		var plcMonitor = new PlcMonitorViewModel(
			Coordinator,
			RecipeMetadataRegistry,
			new HistoricalScheduler());

		var gridStyleEditorViewModelFactory = styleEditorFactory ?? new Func<GridStyleEditorViewModel>(
			() => new GridStyleEditorViewModel(
				new GridStyleEditorFacade(),
				@"C:\does-not-exist",
				AppConfiguration.GridStyle));

		return new MainWindowViewModel(
			Coordinator,
			grid,
			commands,
			clipboard,
			recipeFile,
			MessagePanel,
			columnBuilder,
			plcMonitor,
			gridStyleEditorViewModelFactory,
			NullLogger<MainWindowViewModel>.Instance);
	}

	public void SetSyncEnabled(bool isSyncEnabled)
	{
		PlcSyncService.SetSyncEnabled(isSyncEnabled);
		PlcSyncService.PushPlcState(
			new PlcSessionSnapshot(PlcConnectionState.Disconnected, PlcSyncStatus.Idle, isSyncEnabled));
	}

	public void SetRecipeActive(bool active)
	{
		S7Service.PushExecutionState(PlcExecutionInfo.Empty with { RecipeActive = active });
	}
}
