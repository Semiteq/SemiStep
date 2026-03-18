using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Domain.Facade;

using ReactiveUI;

using Shared.Config;
using Shared.Config.Contracts;

using UI.Services;

namespace UI.ViewModels;

public class MainWindowViewModel : ReactiveObject, IDisposable
{
	private readonly ObservableAsPropertyHelper<bool> _canRedo;
	private readonly ObservableAsPropertyHelper<bool> _canUndo;
	private readonly CompositeDisposable _disposables = new();
	private readonly DomainFacade _domainFacade;

	private readonly ObservableAsPropertyHelper<bool> _isDirty;
	private readonly IShutdownService _shutdownService;
	private readonly Subject<Unit> _stateChanged = new();
	private readonly ObservableAsPropertyHelper<string> _statusText;
	private readonly ObservableAsPropertyHelper<string> _windowTitle;

	public MainWindowViewModel(
		AppConfiguration configuration,
		DomainFacade domainFacade,
		IActionRegistry actionRegistry,
		IGroupRegistry groupRegistry,
		IColumnRegistry columnRegistry,
		IPropertyRegistry propertyRegistry,
		INotificationService notificationService,
		IShutdownService shutdownService)
	{
		Configuration = configuration;
		_domainFacade = domainFacade;
		var notificationService1 = notificationService;
		_shutdownService = shutdownService;

		LogPanel = new LogPanelViewModel();
		ShowMessageInteraction = new Interaction<(string Title, string Message), Unit>();

		RecipeGrid = new RecipeGridViewModel(
			domainFacade,
			actionRegistry,
			groupRegistry,
			columnRegistry,
			propertyRegistry,
			LogPanel,
			notificationService,
			NotifyStateChanged);

		Clipboard = new ClipboardViewModel(domainFacade, RecipeGrid, notificationService);

		RecipeFile = new RecipeFileViewModel(
			domainFacade,
			RecipeGrid,
			LogPanel,
			notificationService,
			NotifyStateChanged);

		var stateObservable = _stateChanged
			.ObserveOn(RxApp.MainThreadScheduler)
			.Publish()
			.RefCount();

		_isDirty = stateObservable
			.Select(_ => _domainFacade.IsDirty)
			.ToProperty(this, x => x.IsDirty)
			.DisposeWith(_disposables);

		_canUndo = stateObservable
			.Select(_ => _domainFacade.CanUndo)
			.ToProperty(this, x => x.CanUndo)
			.DisposeWith(_disposables);

		_canRedo = stateObservable
			.Select(_ => _domainFacade.CanRedo)
			.ToProperty(this, x => x.CanRedo)
			.DisposeWith(_disposables);

		_statusText = stateObservable
			.Select(_ => _domainFacade.IsDirty ? "Modified" : "Saved")
			.ToProperty(this, x => x.StatusText, initialValue: "Saved")
			.DisposeWith(_disposables);

		_windowTitle = stateObservable
			.Select(_ => BuildWindowTitle())
			.ToProperty(this, x => x.WindowTitle, initialValue: BuildWindowTitle())
			.DisposeWith(_disposables);

		ExitCommand = ReactiveCommand.Create(ExecuteExit);

		Observable.FromEvent(
				handler => _domainFacade.ConnectionStateChanged += handler,
				handler => _domainFacade.ConnectionStateChanged -= handler)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(_ =>
			{
				this.RaisePropertyChanged(nameof(IsConnectedToPlc));
				this.RaisePropertyChanged(nameof(ConnectionStatus));

				if (_domainFacade.LastConnectionError is not null)
				{
					notificationService1.ShowError(
						$"PLC connection failed: {_domainFacade.LastConnectionError}");
				}
			})
			.DisposeWith(_disposables);

		_stateChanged.DisposeWith(_disposables);
	}

	public RecipeGridViewModel RecipeGrid { get; }

	public ClipboardViewModel Clipboard { get; }

	public RecipeFileViewModel RecipeFile { get; }

	public LogPanelViewModel LogPanel { get; }

	public AppConfiguration Configuration { get; }

	public Interaction<(string Title, string Message), Unit> ShowMessageInteraction { get; }

	public ReactiveCommand<Unit, Unit> ExitCommand { get; }

	public string WindowTitle => _windowTitle.Value;

	public bool IsDirty => _isDirty.Value;

	public bool CanUndo => _canUndo.Value;

	public bool CanRedo => _canRedo.Value;

	public bool IsConnectedToPlc => _domainFacade.IsConnected;

	public string StatusText => _statusText.Value;

	public string ConnectionStatus => IsConnectedToPlc ? "Connected" : "Disconnected";

	public void Dispose()
	{
		RecipeGrid.Dispose();
		Clipboard.Dispose();
		RecipeFile.Dispose();
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	public void Initialize()
	{
		RecipeGrid.Initialize();
		NotifyStateChanged();
	}

	private void ExecuteExit()
	{
		_shutdownService.Shutdown();
	}

	private void NotifyStateChanged()
	{
		_stateChanged.OnNext(Unit.Default);
	}

	private string BuildWindowTitle()
	{
		var fileName = RecipeFile.CurrentFilePath is not null
			? Path.GetFileNameWithoutExtension(RecipeFile.CurrentFilePath)
			: "New Recipe";
		var dirtyIndicator = _domainFacade.IsDirty ? " *" : "";

		return $"SemiStep - {fileName}{dirtyIndicator}";
	}
}
