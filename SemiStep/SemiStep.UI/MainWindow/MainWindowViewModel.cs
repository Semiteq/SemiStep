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
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.ShutdownService;

namespace SemiStep.UI.MainWindow;

public class MainWindowViewModel : ReactiveObject, IDisposable
{
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly ILogger<MainWindowViewModel> _logger;

	public MainWindowViewModel(
		RecipeCoordinator coordinator,
		RecipeGridViewModel recipeGrid,
		RecipeCommandsViewModel recipeCommands,
		ClipboardViewModel clipboard,
		RecipeFileViewModel recipeFile,
		MessagePanelViewModel messagePanel,
		ColumnBuilder columnBuilder,
		PlcMonitorViewModel plcMonitor,
		ILogger<MainWindowViewModel> logger)
	{
		_coordinator = coordinator;
		_logger = logger;
		RecipeGrid = recipeGrid;
		RecipeCommands = recipeCommands;
		Clipboard = clipboard;
		RecipeFile = recipeFile;
		MessagePanel = messagePanel;
		ColumnBuilder = columnBuilder;
		PlcMonitor = plcMonitor;

		ExitCommand = ReactiveCommand.Create(ExecuteExit);
		ToggleSyncCommand = ReactiveCommand.CreateFromTask(ExecuteToggleSyncAsync);

		ToggleSyncCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(ex => messagePanel.ReportError($"Sync toggle failed: {ex.Message}"))
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

	public bool IsConnectedToPlc => _coordinator.IsConnected;

	public string ConnectionStatus => IsConnectedToPlc ? "Connected" : "Disconnected";

	public string WindowTitle => BuildWindowTitle();

	public bool IsDirty => _coordinator.IsDirty;
	public bool CanUndo => _coordinator.CanUndo;
	public bool CanRedo => _coordinator.CanRedo;

	public bool IsSyncEnabled => _coordinator.IsSyncEnabled;

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
		this.RaisePropertyChanged(nameof(ConnectionStatus));
		this.RaisePropertyChanged(nameof(IsSyncEnabled));
		this.RaisePropertyChanged(nameof(PlcSyncStatusText));
		this.RaisePropertyChanged(nameof(LastSyncTimeText));
	}

	private static string MapSyncStatus(PlcSyncStatus status)
	{
		return status switch
		{
			PlcSyncStatus.Idle => "Idle",
			PlcSyncStatus.Syncing => "Syncing...",
			PlcSyncStatus.Synced => "Synced",
			PlcSyncStatus.OutOfSync => "Out of sync",
			PlcSyncStatus.Failed => "Failed",
			_ => status.ToString()
		};
	}

	private static string FormatLastSyncTime(DateTimeOffset? lastSyncTime)
	{
		if (lastSyncTime is null)
		{
			return "Never";
		}

		var elapsed = (DateTimeOffset.UtcNow - lastSyncTime.Value).TotalSeconds;

		return $"{elapsed:0.0} s ago";
	}

	private string BuildWindowTitle()
	{
		var fileName = RecipeFile.CurrentFilePath is not null
			? Path.GetFileNameWithoutExtension(RecipeFile.CurrentFilePath)
			: "New Recipe";
		var dirtyIndicator = IsDirty ? " *" : "";

		return $"SemiStep - {fileName}{dirtyIndicator}";
	}
}
