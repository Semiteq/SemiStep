using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Domain.Facade;

using ReactiveUI;

using Shared;
using Shared.Registries;

using UI.Models;
using UI.Services;

namespace UI.ViewModels;

public class MainWindowViewModel : ReactiveObject, IDisposable
{
	private readonly IColumnRegistry _columnRegistry;
	private readonly DomainFacade _domainFacade;
	private readonly INotificationService _notificationService;
	private readonly Action _shutdownApplication;
	private readonly CompositeDisposable _disposables = new();
	private readonly Subject<Unit> _stateChanged = new();
	private string? _currentFilePath;
	private int _selectedRowIndex = -1;

	private readonly ObservableAsPropertyHelper<bool> _isDirty;
	private readonly ObservableAsPropertyHelper<bool> _canUndo;
	private readonly ObservableAsPropertyHelper<bool> _canRedo;
	private readonly ObservableAsPropertyHelper<bool> _canDeleteStep;
	private readonly ObservableAsPropertyHelper<string> _statusText;
	private readonly ObservableAsPropertyHelper<string> _windowTitle;

	public MainWindowViewModel(
		AppConfiguration configuration,
		DomainFacade domainFacade,
		IActionRegistry actionRegistry,
		IGroupRegistry groupRegistry,
		IColumnRegistry columnRegistry,
		INotificationService notificationService,
		Action shutdownApplication)
	{
		Configuration = configuration;
		_domainFacade = domainFacade;
		ActionRegistry = actionRegistry;
		GroupRegistry = groupRegistry;
		_columnRegistry = columnRegistry;
		_notificationService = notificationService;
		_shutdownApplication = shutdownApplication;

		RecipeRows = new ObservableCollection<RecipeRowViewModel>();
		LogPanel = new LogPanelViewModel();

		OpenFileInteraction = new Interaction<Unit, string?>();
		SaveFileInteraction = new Interaction<string?, string?>();
		ShowMessageInteraction = new Interaction<(string Title, string Message), Unit>();

		_canDeleteStep = this
			.WhenAnyValue(x => x.SelectedRowIndex)
			.Select(index => index >= 0)
			.ToProperty(this, x => x.CanDeleteStep)
			.DisposeWith(_disposables);

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

		var canUndo = this.WhenAnyValue(x => x.CanUndo);
		var canRedo = this.WhenAnyValue(x => x.CanRedo);

		AddStepCommand = ReactiveCommand.Create(AddStep);
		DeleteStepCommand = ReactiveCommand.Create(DeleteStep, this.WhenAnyValue(x => x.CanDeleteStep));
		SaveRecipeCommand = ReactiveCommand.CreateFromTask(SaveRecipeAsync);
		SaveAsRecipeCommand = ReactiveCommand.CreateFromTask(SaveAsRecipeAsync);
		LoadRecipeCommand = ReactiveCommand.CreateFromTask(LoadRecipeAsync);
		NewRecipeCommand = ReactiveCommand.Create(NewRecipe);
		UndoCommand = ReactiveCommand.Create(Undo, canUndo);
		RedoCommand = ReactiveCommand.Create(Redo, canRedo);
		ExitCommand = ReactiveCommand.Create(ExecuteExit);

		_currentFilePath = null;

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
					_notificationService.ShowError(
						$"PLC connection failed: {_domainFacade.LastConnectionError}");
				}
			})
			.DisposeWith(_disposables);

		_stateChanged.DisposeWith(_disposables);
	}

	public IActionRegistry ActionRegistry { get; }

	public IGroupRegistry GroupRegistry { get; }

	public ObservableCollection<RecipeRowViewModel> RecipeRows { get; }

	public LogPanelViewModel LogPanel { get; }

	public AppConfiguration Configuration { get; }

	public Interaction<Unit, string?> OpenFileInteraction { get; }

	public Interaction<string?, string?> SaveFileInteraction { get; }

	public Interaction<(string Title, string Message), Unit> ShowMessageInteraction { get; }

	public ReactiveCommand<Unit, Unit> AddStepCommand { get; }

	public ReactiveCommand<Unit, Unit> DeleteStepCommand { get; }

	public ReactiveCommand<Unit, Unit> SaveRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> SaveAsRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> LoadRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> NewRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> UndoCommand { get; }

	public ReactiveCommand<Unit, Unit> RedoCommand { get; }

	public ReactiveCommand<Unit, Unit> ExitCommand { get; }

	public string WindowTitle => _windowTitle.Value;

	public bool IsDirty => _isDirty.Value;

	public bool CanUndo => _canUndo.Value;

	public bool CanRedo => _canRedo.Value;

	public int SelectedRowIndex
	{
		get => _selectedRowIndex;
		set => this.RaiseAndSetIfChanged(ref _selectedRowIndex, value);
	}

	public bool CanDeleteStep => _canDeleteStep.Value;

	public bool IsConnectedToPlc => _domainFacade.IsConnected;

	public string StatusText => _statusText.Value;

	public string ConnectionStatus => IsConnectedToPlc ? "Connected" : "Disconnected";

	public void Initialize()
	{
		RefreshRecipeRows();
		LogPanel.RefreshReasons(_domainFacade.Snapshot.Reasons);
		NotifyStateChanged();
	}

	public void Dispose()
	{
		_disposables.Dispose();

		foreach (var row in RecipeRows)
		{
			row.Dispose();
		}
	}

	private void AddStep()
	{
		var firstAction = ActionRegistry.GetAll().First();
		int newRowIndex;

		if (SelectedRowIndex >= 0)
		{
			newRowIndex = SelectedRowIndex + 1;
			_domainFacade.InsertStep(newRowIndex, firstAction.Id);
		}
		else
		{
			newRowIndex = RecipeRows.Count;
			_domainFacade.AppendStep(firstAction.Id);
		}

		RefreshAfterMutation();
		SelectedRowIndex = newRowIndex;
	}

	private void DeleteStep()
	{
		if (SelectedRowIndex < 0)
		{
			return;
		}

		var indexToDelete = SelectedRowIndex;
		_domainFacade.RemoveStep(indexToDelete);

		RefreshAfterMutation();

		if (RecipeRows.Count > 0)
		{
			SelectedRowIndex = Math.Min(indexToDelete, RecipeRows.Count - 1);
		}
		else
		{
			SelectedRowIndex = -1;
		}
	}

	private async Task SaveRecipeAsync()
	{
		if (_currentFilePath is not null)
		{
			await SaveToFileAsync(_currentFilePath);

			return;
		}

		await SaveAsRecipeAsync();
	}

	private async Task SaveAsRecipeAsync()
	{
		var suggestedName = _currentFilePath is not null
			? Path.GetFileNameWithoutExtension(_currentFilePath)
			: null;

		var filePath = await SaveFileInteraction.Handle(suggestedName);
		if (filePath is null)
		{
			return;
		}

		await SaveToFileAsync(filePath);
	}

	private async Task SaveToFileAsync(string filePath)
	{
		try
		{
			await _domainFacade.SaveRecipeAsync(filePath);
			_currentFilePath = filePath;
			NotifyStateChanged();
			_notificationService.ShowSuccess($"Saved: {Path.GetFileName(filePath)}");
		}
		catch (Exception ex)
		{
			_notificationService.ShowError($"Failed to save recipe: {ex.Message}");
		}
	}

	private async Task LoadRecipeAsync()
	{
		var filePath = await OpenFileInteraction.Handle(Unit.Default);
		if (filePath is null)
		{
			return;
		}

		try
		{
			await _domainFacade.LoadRecipeAsync(filePath);
			_currentFilePath = filePath;
			RefreshAfterMutation();
			_notificationService.ShowSuccess($"Loaded: {Path.GetFileName(filePath)}");
		}
		catch (Exception ex)
		{
			_notificationService.ShowError($"Failed to load recipe: {ex.Message}");
		}
	}

	private void NewRecipe()
	{
		_domainFacade.NewRecipe();
		_currentFilePath = null;

		LogPanel.Clear();
		RefreshAfterMutation();
	}

	private void Undo()
	{
		var snapshot = _domainFacade.Undo();
		if (snapshot is not null)
		{
			RefreshAfterMutation();
		}
	}

	private void Redo()
	{
		var snapshot = _domainFacade.Redo();
		if (snapshot is not null)
		{
			RefreshAfterMutation();
		}
	}

	private void ExecuteExit()
	{
		_shutdownApplication();
	}

	private void RefreshAfterMutation()
	{
		RefreshRecipeRows();
		LogPanel.RefreshReasons(_domainFacade.Snapshot.Reasons);
		NotifyStateChanged();
	}

	private void RefreshRecipeRows()
	{
		var recipe = _domainFacade.CurrentRecipe;

		foreach (var row in RecipeRows)
		{
			row.Dispose();
		}

		RecipeRows.Clear();

		for (var i = 0; i < recipe.StepCount; i++)
		{
			var step = recipe.Steps[i];
			var action = ActionRegistry.GetAction(step.ActionKey);
			var rowVm = new RecipeRowViewModel(
				i + 1,
				step,
				action,
				GroupRegistry,
				_columnRegistry,
				OnCellValueChanged,
				OnActionChanged);
			RecipeRows.Add(rowVm);
		}
	}

	private void OnCellValueChanged(int stepIndex, string columnKey, object? value)
	{
		if (value is null)
		{
			return;
		}

		try
		{
			_domainFacade.UpdateStepProperty(stepIndex, columnKey, value);

			var updatedStep = _domainFacade.CurrentRecipe.Steps[stepIndex];
			RecipeRows[stepIndex].UpdateStep(updatedStep);
		}
		catch (Exception ex)
		{
			_notificationService.ShowError($"Step {stepIndex + 1}: {ex.Message}");
		}

		LogPanel.RefreshReasons(_domainFacade.Snapshot.Reasons);
		NotifyStateChanged();
	}

	private void OnActionChanged(int stepIndex, int newActionId)
	{
		try
		{
			_domainFacade.ChangeStepAction(stepIndex, newActionId);
			RefreshRecipeRows();
		}
		catch (Exception ex)
		{
			_notificationService.ShowError(
				$"Step {stepIndex + 1}: Failed to change action - {ex.Message}");
		}

		LogPanel.RefreshReasons(_domainFacade.Snapshot.Reasons);
		NotifyStateChanged();
	}

	private void NotifyStateChanged()
	{
		_stateChanged.OnNext(Unit.Default);
	}

	private string BuildWindowTitle()
	{
		var fileName = _currentFilePath is not null
			? Path.GetFileNameWithoutExtension(_currentFilePath)
			: "New Recipe";
		var dirtyIndicator = _domainFacade.IsDirty ? " *" : "";

		return $"SemiStep - {fileName}{dirtyIndicator}";
	}
}
