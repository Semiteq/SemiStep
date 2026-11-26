using System.Reactive.Concurrency;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Plc.Sync.Ownership;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.UI;

using SemiStep.UI.Clipboard;
using SemiStep.UI.Coordinator;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.RecipeGrid.Transposed;
using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI.Helpers;

public sealed class UIFixture : IAsyncLifetime
{
	private readonly List<IDisposable> _ownedDisposables = [];

	public RecipeSession Session { get; private set; } = null!;
	public PlcLifecycleManager Plc { get; private set; } = null!;
	public StubPlcSyncService PlcSyncService { get; private set; } = null!;
	public StubS7Service S7Service { get; private set; } = null!;
	public RecipeMetadataRegistry RecipeMetadataRegistry { get; private set; } = null!;
	public ComboBoxItemsProvider ComboBoxItemsProvider { get; private set; } = null!;
	public AppConfiguration AppConfiguration { get; private set; } = null!;
	public MessagePanelViewModel MessagePanel { get; private set; } = null!;
	public CsvService CsvService { get; private set; } = null!;
	public RecipeCoordinator Coordinator { get; private set; } = null!;
	public StubS7Service StubS7 { get; private set; } = null!;
	public StubPlcSyncOwnership SyncOwnership { get; private set; } = null!;
	public ColumnBuilder ColumnBuilder { get; private set; } = null!;

	// Shared across every surface the fixture creates, mirroring the DI singleton: the
	// click-away clear must reach sibling surfaces built from the same fixture.
	public ChangeMarkClearBroadcaster ChangeMarkClearBroadcaster { get; } = new();

	public HistoricalScheduler Scheduler { get; } = new(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

	public ValueTask InitializeAsync()
	{
		return InitializeAsync("WithGroups");
	}

	public async ValueTask InitializeAsync(string configName)
	{
		var (services, session, plc) = await CoreTestHelper.BuildAsync(configName);
		Session = session;
		Plc = plc;
		PlcSyncService = (StubPlcSyncService)services.GetRequiredService<IPlcSyncService>();
		S7Service = services.GetRequiredService<StubS7Service>();
		RecipeMetadataRegistry = services.GetRequiredService<RecipeMetadataRegistry>();
		ComboBoxItemsProvider = new ComboBoxItemsProvider(RecipeMetadataRegistry);
		AppConfiguration = services.GetRequiredService<AppConfiguration>();
		MessagePanel = new MessagePanelViewModel();
		StubS7 = services.GetRequiredService<StubS7Service>();
		SyncOwnership = (StubPlcSyncOwnership)services.GetRequiredService<IPlcSyncOwnership>();
		CsvService = services.GetRequiredService<CsvService>();
		Coordinator = new RecipeCoordinator(
			Session,
			Plc,
			CsvService,
			AppConfiguration,
			RecipeMetadataRegistry,
			MessagePanel,
			NullLogger<RecipeCoordinator>.Instance);
		Coordinator.Initialize();
		ColumnBuilder = new ColumnBuilder(AppConfiguration.GridStyle, RecipeMetadataRegistry, ComboBoxItemsProvider);
	}

	// Surface disposal is idempotent, so suites that dispose their surfaces explicitly and
	// suites that rely on this teardown (e.g. via CreateMainWindowViewModel) both stay clean.
	public ValueTask DisposeAsync()
	{
		foreach (var owned in _ownedDisposables)
		{
			owned.Dispose();
		}

		Coordinator.Dispose();
		MessagePanel.Dispose();
		return ValueTask.CompletedTask;
	}

	public CanonicalRecipeGridSurface CreateCanonicalSurface(
		ILogger<CanonicalRecipeGridSurface>? logger = null)
	{
		var surface = new CanonicalRecipeGridSurface(
			Coordinator,
			RecipeMetadataRegistry,
			ComboBoxItemsProvider,
			ColumnBuilder,
			MessagePanel,
			ChangeMarkClearBroadcaster,
			logger ?? NullLogger<CanonicalRecipeGridSurface>.Instance);
		_ownedDisposables.Add(surface);

		return surface;
	}

	public TransposedRecipeGridSurface CreateTransposedSurface(
		ILogger<TransposedRecipeGridSurface>? logger = null)
	{
		var surface = new TransposedRecipeGridSurface(
			Coordinator,
			RecipeMetadataRegistry,
			ComboBoxItemsProvider,
			AppConfiguration.GridStyle,
			MessagePanel,
			ChangeMarkClearBroadcaster,
			logger ?? NullLogger<TransposedRecipeGridSurface>.Instance);
		_ownedDisposables.Add(surface);

		return surface;
	}

	public ActiveRecipeGridSurface CreateActiveSurface(GridStyleOptions? gridStyle = null)
	{
		return new ActiveRecipeGridSurface(
			CreateCanonicalSurface(),
			CreateTransposedSurface(),
			gridStyle ?? AppConfiguration.GridStyle);
	}

	public void SeedRecipe(int stepCount)
	{
		Coordinator.NewRecipe();
		for (var i = 0; i < stepCount; i++)
		{
			Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
		}
	}

	public MainWindowViewModel CreateMainWindowViewModel(
		Func<GridStyleEditorViewModel>? styleEditorFactory = null,
		ILogger<MainWindowViewModel>? logger = null)
	{
		var grid = CreateActiveSurface();

		var commands = new RecipeCommandsViewModel(
			Coordinator,
			grid,
			MessagePanel,
			NullLogger<RecipeCommandsViewModel>.Instance);

		var clipboardSerializer = new ClipboardSerializer(
			RecipeMetadataRegistry,
			NullLogger<ClipboardSerializer>.Instance);
		var importedRecipeValidator = new ImportedRecipeValidator(RecipeMetadataRegistry);
		var clipboard = new ClipboardViewModel(
			Coordinator,
			grid,
			clipboardSerializer,
			importedRecipeValidator,
			MessagePanel,
			NullLogger<ClipboardViewModel>.Instance);

		var recipeFile = new RecipeFileViewModel(
			Coordinator,
			MessagePanel,
			NullLogger<RecipeFileViewModel>.Instance);

		var plcMonitor = new PlcMonitorViewModel(
			Coordinator,
			RecipeMetadataRegistry,
			Scheduler);
		_ownedDisposables.Add(plcMonitor);

		var gridStyleEditorViewModelFactory = styleEditorFactory ?? new Func<GridStyleEditorViewModel>(
			() => new GridStyleEditorViewModel(
				new GridStyleEditorFacade(),
				@"C:\does-not-exist",
				AppConfiguration.GridStyle,
				NullLogger<GridStyleEditorViewModel>.Instance));

		return new MainWindowViewModel(
			Coordinator,
			grid,
			commands,
			clipboard,
			recipeFile,
			MessagePanel,
			plcMonitor,
			new PlcLinkAddress(
				AppConfiguration.PlcConfiguration.Connection.IpAddress,
				AppConfiguration.PlcConfiguration.Connection.Port,
				IsOverride: false),
			gridStyleEditorViewModelFactory,
			Scheduler,
			logger ?? NullLogger<MainWindowViewModel>.Instance);
	}

	/// <summary>Saves the current recipe to a CSV file and opens it again, so its values come from a file and the history is empty.</summary>
	public async Task ReloadFromCsvAsync()
	{
		var path = Path.Combine(Path.GetTempPath(), $"semistep-reload-{Guid.NewGuid():N}.csv");
		try
		{
			(await Coordinator.SaveRecipeAsync(path)).IsSuccess.Should().BeTrue();
			(await Coordinator.LoadRecipeAsync(path)).IsSuccess.Should().BeTrue();
		}
		finally
		{
			File.Delete(path);
		}
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
