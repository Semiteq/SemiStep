using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Controls;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

using SemiStep.UI.Clipboard;
using SemiStep.UI.Coordinator;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.ShutdownService;
using SemiStep.UI.StyleEditor;

namespace SemiStep.UI.MainWindow;

public class MainWindowViewModel : ReactiveObject, IDisposable
{
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly ILogger<MainWindowViewModel> _logger;
	private readonly Func<GridStyleEditorViewModel> _gridStyleEditorViewModelFactory;

	private bool _isToolBarVisible = true;

	public MainWindowViewModel(
		RecipeCoordinator coordinator,
		RecipeGridViewModel recipeGrid,
		RecipeCommandsViewModel recipeCommands,
		ClipboardViewModel clipboard,
		RecipeFileViewModel recipeFile,
		MessagePanelViewModel messagePanel,
		ColumnBuilder columnBuilder,
		PlcMonitorViewModel plcMonitor,
		Func<GridStyleEditorViewModel> gridStyleEditorViewModelFactory,
		ILogger<MainWindowViewModel> logger)
	{
		_coordinator = coordinator;
		_logger = logger;
		_gridStyleEditorViewModelFactory = gridStyleEditorViewModelFactory;
		RecipeGrid = recipeGrid;
		RecipeCommands = recipeCommands;
		Clipboard = clipboard;
		RecipeFile = recipeFile;
		MessagePanel = messagePanel;
		ColumnBuilder = columnBuilder;
		PlcMonitor = plcMonitor;

		ExitCommand = ReactiveCommand.Create(ExecuteExit);
		ToggleSyncCommand = ReactiveCommand.CreateFromTask(ExecuteToggleSyncAsync);
		OpenStyleEditorCommand = ReactiveCommand.CreateFromTask(ExecuteOpenStyleEditorAsync);
		ToggleToolBarCommand = ReactiveCommand.Create(ExecuteToggleToolBar);

		ToggleSyncCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(ex => messagePanel.ReportError($"Sync toggle failed: {ex.Message}"))
			.DisposeWith(_disposables);

		OpenStyleEditorCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(ex => messagePanel.ReportError($"Style editor failed: {ex.Message}"))
			.DisposeWith(_disposables);

		_coordinator.Mutated += OnCoordinatorMutated;
		_disposables.Add(Disposable.Create(() => _coordinator.Mutated -= OnCoordinatorMutated));

		_coordinator.PlcStateChanged
			.Subscribe(_ => RaiseConnectionStateProperties())
			.DisposeWith(_disposables);

		_coordinator.PlcRecipeConflictDetected
			.Subscribe(conflict => _ = HandleConflictAsync(conflict.Local, conflict.Plc))
			.DisposeWith(_disposables);

		Observable.Interval(TimeSpan.FromSeconds(1))
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(_ => this.RaisePropertyChanged(nameof(LastSyncTimeText)))
			.DisposeWith(_disposables);
	}

	public Window? MainWindow { get; set; }

	public RecipeGridViewModel RecipeGrid { get; }

	public RecipeCommandsViewModel RecipeCommands { get; }

	public ClipboardViewModel Clipboard { get; }

	public RecipeFileViewModel RecipeFile { get; }

	public MessagePanelViewModel MessagePanel { get; }

	public ColumnBuilder ColumnBuilder { get; }

	public PlcMonitorViewModel PlcMonitor { get; }

	public ReactiveCommand<Unit, Unit> ExitCommand { get; }

	public ReactiveCommand<Unit, Unit> ToggleSyncCommand { get; }

	public ReactiveCommand<Unit, Unit> OpenStyleEditorCommand { get; }

	public ReactiveCommand<Unit, Unit> ToggleToolBarCommand { get; }

	public bool IsToolBarVisible
	{
		get => _isToolBarVisible;
		private set => this.RaiseAndSetIfChanged(ref _isToolBarVisible, value);
	}

	public bool IsConnectedToPlc => _coordinator.IsConnected;

	public string WindowTitle => BuildWindowTitle();

	public bool IsDirty => _coordinator.IsDirty;
	public bool CanUndo => _coordinator.CanUndo;
	public bool CanRedo => _coordinator.CanRedo;

	public bool IsSyncEnabled => _coordinator.IsSyncEnabled;

	public bool IsSyncLocalMode => !IsSyncEnabled;

	public bool IsSyncConnecting => IsSyncEnabled && _coordinator.IsConnecting;

	public bool IsSyncNoLink => IsSyncEnabled && !IsConnectedToPlc && !_coordinator.IsConnecting;

	public bool IsSyncLinked => IsSyncEnabled && IsConnectedToPlc && !_coordinator.IsConnecting;

	public bool IsSyncOnIdle => IsSyncEnabled && !IsSyncConnecting;

	public string PlcSyncStatusText => MapSyncStatus(_coordinator.SyncStatus);

	public string LastSyncTimeText => FormatLastSyncTime(_coordinator.LastSyncTime);

	public void Dispose()
	{
		PlcMonitor.Dispose();
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	public void Initialize()
	{
		RecipeGrid.Initialize();
		RaiseAllStateProperties();
	}

	private static void ExecuteExit()
	{
		DesktopShutdownService.Shutdown();
	}

	private void ExecuteToggleToolBar()
	{
		IsToolBarVisible = !IsToolBarVisible;
	}

	private async Task ExecuteOpenStyleEditorAsync()
	{
		if (MainWindow is null)
		{
			return;
		}

		var viewModel = _gridStyleEditorViewModelFactory();
		await viewModel.LoadAsync();

		var window = new GridStyleEditorWindow { DataContext = viewModel };
		await window.ShowDialog(MainWindow);
	}

	private async Task ExecuteToggleSyncAsync()
	{
		if (_coordinator.IsSyncEnabled)
		{
			await _coordinator.DisableSync();
		}
		else
		{
			var result = await _coordinator.EnableSync();

			if (result.IsFailed)
			{
				MessagePanel.ReportFailure(result);
			}
		}

		RaiseConnectionStateProperties();
	}

	private async Task HandleConflictAsync(Recipe local, Recipe plc)
	{
		if (MainWindow is null)
		{
			return;
		}

		var viewModel = new PlcConflictDialogViewModel(local.StepCount, plc.StepCount);
		var dialog = new PlcConflictDialog(viewModel);

		try
		{
			await dialog.ShowDialog(MainWindow);
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error while showing PLC conflict dialog: {Message}", ex.Message);
			MessagePanel.ReportError("Failed to show PLC conflict dialog");

			return;
		}

		if (!dialog.Confirmed)
		{
			return;
		}

		var result = _coordinator.ResolveConflict(dialog.KeepLocal);

		if (result.IsFailed)
		{
			MessagePanel.ReportFailure(result);
		}
	}

	private void OnCoordinatorMutated(MutationSignal signal)
	{
		_ = signal;
		RaiseAllStateProperties();
	}

	private void RaiseAllStateProperties()
	{
		this.RaisePropertyChanged(nameof(WindowTitle));
		this.RaisePropertyChanged(nameof(IsDirty));
		this.RaisePropertyChanged(nameof(CanUndo));
		this.RaisePropertyChanged(nameof(CanRedo));
		RaiseConnectionStateProperties();
	}

	private void RaiseConnectionStateProperties()
	{
		this.RaisePropertyChanged(nameof(IsConnectedToPlc));
		this.RaisePropertyChanged(nameof(IsSyncEnabled));
		this.RaisePropertyChanged(nameof(IsSyncLocalMode));
		this.RaisePropertyChanged(nameof(IsSyncConnecting));
		this.RaisePropertyChanged(nameof(IsSyncNoLink));
		this.RaisePropertyChanged(nameof(IsSyncLinked));
		this.RaisePropertyChanged(nameof(IsSyncOnIdle));
		this.RaisePropertyChanged(nameof(PlcSyncStatusText));
		this.RaisePropertyChanged(nameof(LastSyncTimeText));
	}

	internal static string MapSyncStatus(PlcSyncStatus status)
	{
		return status switch
		{
			PlcSyncStatus.Idle => Resources.StatusIdle,
			PlcSyncStatus.Syncing => Resources.StatusSyncing,
			PlcSyncStatus.Synced => Resources.StatusSynced,
			PlcSyncStatus.OutOfSync => string.Empty,
			PlcSyncStatus.Failed => Resources.StatusFailed,
			_ => status.ToString()
		};
	}

	internal static string FormatLastSyncTime(DateTimeOffset? lastSyncTime)
	{
		var value = lastSyncTime is null
			? Resources.LastSyncNever
			: string.Format(
				CultureInfo.InvariantCulture,
				Resources.LastSyncAgoFormat,
				(DateTimeOffset.UtcNow - lastSyncTime.Value).TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture));

		return string.Format(CultureInfo.InvariantCulture, Resources.LastSyncPrefix, value);
	}

	private string BuildWindowTitle()
	{
		var fileName = RecipeFile.CurrentFilePath is not null
			? Path.GetFileNameWithoutExtension(RecipeFile.CurrentFilePath)
			: Resources.WindowTitleNewRecipe;
		var dirtyIndicator = IsDirty ? " *" : "";

		return $"SemiStep - {fileName}{dirtyIndicator}";
	}
}
