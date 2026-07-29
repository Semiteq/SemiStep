using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

using SemiStep.UI.Clipboard;
using SemiStep.UI.Coordinator;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.RecipeGrid;
using SemiStep.UI.StyleEditor;

namespace SemiStep.UI.MainWindow;

public class MainWindowViewModel : ReactiveObject, IDisposable
{
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly ILogger<MainWindowViewModel> _logger;
	private readonly Func<GridStyleEditorViewModel> _gridStyleEditorViewModelFactory;
	private readonly ObservableAsPropertyHelper<bool> _isTransposedOrientation;

	private bool _isToolBarVisible = true;

	public MainWindowViewModel(
		RecipeCoordinator coordinator,
		ActiveRecipeGridSurface recipeGrid,
		RecipeCommandsViewModel recipeCommands,
		ClipboardViewModel clipboard,
		RecipeFileViewModel recipeFile,
		MessagePanelViewModel messagePanel,
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
		PlcMonitor = plcMonitor;

		ShowStyleEditorInteraction = new Interaction<GridStyleEditorViewModel, Unit>();
		ResolveConflictInteraction = new Interaction<PlcConflictDialogViewModel, bool?>();
		RequestCloseInteraction = new Interaction<Unit, Unit>();

		ExitCommand = ReactiveCommand.CreateFromTask(RequestCloseAsync);
		ToggleSyncCommand = ReactiveCommand.CreateFromTask(ExecuteToggleSyncAsync);
		OpenStyleEditorCommand = ReactiveCommand.CreateFromTask(ExecuteOpenStyleEditorAsync);
		ToggleToolBarCommand = ReactiveCommand.Create(ExecuteToggleToolBar);
		ToggleOrientationCommand = ReactiveCommand.Create(recipeGrid.ToggleOrientation);

		_isTransposedOrientation = recipeGrid
			.WhenAnyValue(x => x.Orientation)
			.Select(orientation => orientation == GridOrientation.ColumnsAsSteps)
			.ToProperty(this, x => x.IsTransposedOrientation)
			.DisposeWith(_disposables);

		ToggleSyncCommand.ReportThrownExceptions(MessagePanel, _logger, new LocalizedText(nameof(Resources.SyncToggleFailed)))
			.DisposeWith(_disposables);

		OpenStyleEditorCommand.ReportThrownExceptions(MessagePanel, _logger, new LocalizedText(nameof(Resources.StyleEditorFailed)))
			.DisposeWith(_disposables);

		ExitCommand.ReportThrownExceptions(MessagePanel, _logger, new LocalizedText(nameof(Resources.ExitFailed)))
			.DisposeWith(_disposables);

		ToggleOrientationCommand.ReportThrownExceptions(MessagePanel, _logger, new LocalizedText(nameof(Resources.OrientationToggleFailed)))
			.DisposeWith(_disposables);

		_coordinator.Mutated += OnCoordinatorMutated;
		_disposables.Add(Disposable.Create(() => _coordinator.Mutated -= OnCoordinatorMutated));

		_coordinator.PlcStateChanged
			.Subscribe(
				_ => Guarded(new LocalizedText(nameof(Resources.PlcStateUpdateFailed)), RaiseConnectionStateProperties),
				OnSubscriptionError(new LocalizedText(nameof(Resources.PlcStateUpdateFailed))))
			.DisposeWith(_disposables);

		_coordinator.PlcRecipeConflictDetected
			.Subscribe(
				conflict => Guarded(
					new LocalizedText(nameof(Resources.PlcConflictHandlingFailed)),
					() => _ = HandleConflictAsync(conflict.Local, conflict.Plc)),
				OnSubscriptionError(new LocalizedText(nameof(Resources.PlcConflictHandlingFailed))))
			.DisposeWith(_disposables);

		Observable.Interval(TimeSpan.FromSeconds(1))
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(
				_ => Guarded(
					new LocalizedText(nameof(Resources.SyncTimeRefreshFailed)),
					() => this.RaisePropertyChanged(nameof(LastSyncTimeText))),
				OnSubscriptionError(new LocalizedText(nameof(Resources.SyncTimeRefreshFailed))))
			.DisposeWith(_disposables);
	}

	public Interaction<GridStyleEditorViewModel, Unit> ShowStyleEditorInteraction { get; }

	// Result: null = cancel, true = keep local, false = load from PLC.
	internal Interaction<PlcConflictDialogViewModel, bool?> ResolveConflictInteraction { get; }

	public Interaction<Unit, Unit> RequestCloseInteraction { get; }

	public IRecipeGridSurface RecipeGrid { get; }

	public RecipeCommandsViewModel RecipeCommands { get; }

	public ClipboardViewModel Clipboard { get; }

	public RecipeFileViewModel RecipeFile { get; }

	public MessagePanelViewModel MessagePanel { get; }

	public PlcMonitorViewModel PlcMonitor { get; }

	public ReactiveCommand<Unit, Unit> ExitCommand { get; }

	public ReactiveCommand<Unit, Unit> ToggleSyncCommand { get; }

	public ReactiveCommand<Unit, Unit> OpenStyleEditorCommand { get; }

	public ReactiveCommand<Unit, Unit> ToggleToolBarCommand { get; }

	public ReactiveCommand<Unit, Unit> ToggleOrientationCommand { get; }

	public bool IsTransposedOrientation => _isTransposedOrientation.Value;

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

	private void ExecuteToggleToolBar()
	{
		IsToolBarVisible = !IsToolBarVisible;
	}

	// Closing routes through the window handler so MainWindow.OnWindowClosing runs its dirty-close guard.
	private async Task RequestCloseAsync()
	{
		await RequestCloseInteraction.Handle(Unit.Default);
	}

	private async Task ExecuteOpenStyleEditorAsync()
	{
		var viewModel = _gridStyleEditorViewModelFactory();
		await viewModel.LoadAsync();

		await ShowStyleEditorInteraction.Handle(viewModel);
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

	internal async Task HandleConflictAsync(Recipe local, Recipe plc)
	{
		bool? keepLocal;
		try
		{
			keepLocal = await ResolveConflictInteraction.Handle(
				new PlcConflictDialogViewModel(local.StepCount, plc.StepCount));
		}
		catch (Exception ex)
		{
			_logger.LogWarning("Unexpected error while showing PLC conflict dialog: {Message}", ex.Message);
			MessagePanel.ReportError(Resources.PlcConflictDialogShowFailed);

			return;
		}

		if (keepLocal is null)
		{
			return;
		}

		// The conflict callback runs fire-and-forget, so a throw in the post-dialog resolution would
		// fault the discarded task and surface only through the nondeterministic TaskScheduler hook.
		Guarded(new LocalizedText(nameof(Resources.PlcConflictResolutionFailed)), () =>
		{
			var result = _coordinator.ResolveConflict(keepLocal.Value);

			if (result.IsFailed)
			{
				MessagePanel.ReportFailure(result);
			}
		});
	}

	private void OnCoordinatorMutated(MutationSignal signal)
	{
		_ = signal;
		RaiseAllStateProperties();
	}

	internal Action<Exception> OnSubscriptionError(LocalizedText context)
	{
		return ex => ExceptionReporter.ReportAndLog(MessagePanel, _logger, context, ex);
	}

	// A throw inside a subscription's onNext body is NOT routed to onError: Rx disposes the
	// subscription and rethrows up the pipeline (fatal for a dispatcher-scheduled tick). Wrapping
	// the body here contains it on the same report + log path as a source-observable error.
	internal void Guarded(LocalizedText context, Action body)
	{
		try
		{
			body();
		}
		catch (Exception ex)
		{
			ExceptionReporter.ReportAndLog(MessagePanel, _logger, context, ex);
		}
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
