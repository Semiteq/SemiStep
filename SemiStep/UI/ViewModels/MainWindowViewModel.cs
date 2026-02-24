using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Linq;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using Domain.Facade;

using ReactiveUI;

using Shared;
using Shared.Reasons;
using Shared.Registries;

using UI.Models;
using UI.Services;

namespace UI.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
	private readonly IColumnRegistry _columnRegistry;
	private readonly string? _currentFileName;
	private readonly DomainFacade _domainFacade;
	private readonly INotificationService _notificationService;
	private int _selectedRowIndex = -1;
	private bool _isLogPanelVisible = true;

	public MainWindowViewModel(
		DomainFacade domainFacade,
		IActionRegistry actionRegistry,
		IGroupRegistry groupRegistry,
		IColumnRegistry columnRegistry,
		INotificationService notificationService)
	{
		_domainFacade = domainFacade;
		ActionRegistry = actionRegistry;
		GroupRegistry = groupRegistry;
		_columnRegistry = columnRegistry;
		_notificationService = notificationService;

		RecipeRows = new ObservableCollection<RecipeRowViewModel>();
		LogEntries = new ObservableCollection<LogEntry>();
		LogEntries.CollectionChanged += OnLogEntriesChanged;

		// File dialog interactions
		OpenFileInteraction = new Interaction<Unit, string?>();
		SaveFileInteraction = new Interaction<string?, string?>();
		ShowMessageInteraction = new Interaction<(string Title, string Message), Unit>();

		AddStepCommand = ReactiveCommand.Create(AddStep);
		DeleteStepCommand = ReactiveCommand.Create(DeleteStep);
		SaveRecipeCommand = ReactiveCommand.CreateFromTask(SaveRecipeAsync);
		LoadRecipeCommand = ReactiveCommand.CreateFromTask(LoadRecipeAsync);
		NewRecipeCommand = ReactiveCommand.Create(NewRecipe);
		UndoCommand = ReactiveCommand.Create(Undo);
		RedoCommand = ReactiveCommand.Create(Redo);
		ExitCommand = ReactiveCommand.Create(Exit);
		ClearLogCommand = ReactiveCommand.Create(ClearLog);
		ToggleLogPanelCommand = ReactiveCommand.Create(ToggleLogPanel);

		_currentFileName = "New Recipe";
	}

	public IActionRegistry ActionRegistry { get; }

	public IGroupRegistry GroupRegistry { get; }

	public ObservableCollection<RecipeRowViewModel> RecipeRows { get; }

	public ObservableCollection<LogEntry> LogEntries { get; }

	public AppConfiguration? Configuration { get; private set; }

	// File dialog interactions - handled by the View
	public Interaction<Unit, string?> OpenFileInteraction { get; }

	public Interaction<string?, string?> SaveFileInteraction { get; }

	public Interaction<(string Title, string Message), Unit> ShowMessageInteraction { get; }

	public ReactiveCommand<Unit, Unit> AddStepCommand { get; }

	public ReactiveCommand<Unit, Unit> DeleteStepCommand { get; }

	public ReactiveCommand<Unit, Unit> SaveRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> LoadRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> NewRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> UndoCommand { get; }

	public ReactiveCommand<Unit, Unit> RedoCommand { get; }

	public ReactiveCommand<Unit, Unit> ExitCommand { get; }

	public ReactiveCommand<Unit, Unit> ClearLogCommand { get; }

	public ReactiveCommand<Unit, Unit> ToggleLogPanelCommand { get; }

	public string WindowTitle
	{
		get
		{
			var fileName = _currentFileName ?? "Untitled";
			var dirtyIndicator = IsDirty ? " *" : "";

			return $"SemiStep - {fileName}{dirtyIndicator}";
		}
	}

	public bool IsDirty => _domainFacade.IsDirty;

	public bool CanUndo => _domainFacade.CanUndo;

	public bool CanRedo => _domainFacade.CanRedo;

	public int SelectedRowIndex
	{
		get => _selectedRowIndex;
		set => this.RaiseAndSetIfChanged(ref _selectedRowIndex, value);
	}

	public bool CanDeleteStep => SelectedRowIndex >= 0;

	public bool IsConnectedToPlc => false;

	public string StatusText => IsDirty ? "Modified" : "Saved";

	public string ConnectionStatus => IsConnectedToPlc ? "Connected" : "Disconnected";

	public bool HasLogEntries => LogEntries.Count > 0;

	public bool HasErrors => LogEntries.Any(e => e.Severity == LogSeverity.Error);

	public bool HasWarnings => LogEntries.Any(e => e.Severity == LogSeverity.Warning);

	public int ErrorCount => LogEntries.Count(e => e.Severity == LogSeverity.Error);

	public int WarningCount => LogEntries.Count(e => e.Severity == LogSeverity.Warning);

	public string ErrorCountText => $"{ErrorCount} {(ErrorCount == 1 ? "Error" : "Errors")}";

	public string WarningCountText => $"{WarningCount} {(WarningCount == 1 ? "Warning" : "Warnings")}";

	public string StatusErrorSummary
	{
		get
		{
			var parts = new List<string>();
			if (ErrorCount > 0)
			{
				parts.Add(ErrorCountText);
			}

			if (WarningCount > 0)
			{
				parts.Add(WarningCountText);
			}

			return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
		}
	}

	public bool HasStatusErrors => ErrorCount > 0 || WarningCount > 0;

	public bool IsLogPanelVisible
	{
		get => _isLogPanelVisible;
		set
		{
			this.RaiseAndSetIfChanged(ref _isLogPanelVisible, value);
			this.RaisePropertyChanged(nameof(ShowLogPanel));
		}
	}

	public bool ShowLogPanel => HasLogEntries && IsLogPanelVisible;

	public void Initialize(AppConfiguration configuration)
	{
		Configuration = configuration;
		RefreshRecipeRows();
		RefreshReasons();
	}

	private void AddStep()
	{
		var firstAction = ActionRegistry.GetAll().First();
		int newRowIndex;

		if (SelectedRowIndex >= 0)
		{
			// Insert after selected row
			newRowIndex = SelectedRowIndex + 1;
			_domainFacade.InsertStep(newRowIndex, firstAction.Id);
		}
		else
		{
			// Append to end
			newRowIndex = RecipeRows.Count; // Current count = index of new row after refresh
			_domainFacade.AppendStep(firstAction.Id);
		}

		RefreshRecipeRows();
		SelectedRowIndex = newRowIndex;
		RefreshReasons();
		RaiseStateChanged();
	}

	private void DeleteStep()
	{
		if (SelectedRowIndex < 0)
		{
			return;
		}

		var indexToDelete = SelectedRowIndex;
		_domainFacade.RemoveStep(indexToDelete);

		RefreshRecipeRows();

		// Keep selection at same position if possible, otherwise select the new last row
		if (RecipeRows.Count > 0)
		{
			SelectedRowIndex = Math.Min(indexToDelete, RecipeRows.Count - 1);
		}
		else
		{
			SelectedRowIndex = -1;
		}

		RefreshReasons();
		RaiseStateChanged();
	}

	private async Task SaveRecipeAsync()
	{
		// Show save file dialog
		var filePath = await SaveFileInteraction.Handle(null);
		if (filePath is null)
		{
			return; // User cancelled
		}

		// TODO: Implement file saving when backend is ready
		// For now, show "Not implemented" message
		await ShowMessageInteraction.Handle(("Save Recipe",
			"File saving is not yet implemented.\n\nSelected path: " + filePath));
	}

	private async Task LoadRecipeAsync()
	{
		// Show open file dialog
		var filePath = await OpenFileInteraction.Handle(Unit.Default);
		if (filePath is null)
		{
			return; // User cancelled
		}

		// TODO: Implement file loading when backend is ready
		// For now, show "Not implemented" message
		await ShowMessageInteraction.Handle(("Open Recipe",
			"File loading is not yet implemented.\n\nSelected path: " + filePath));
	}

	private void NewRecipe()
	{
		_domainFacade.NewRecipe();
		LogEntries.Clear();
		RefreshRecipeRows();
		RefreshReasons();
		RaiseStateChanged();
	}

	private void Undo()
	{
		var snapshot = _domainFacade.Undo();
		if (snapshot is not null)
		{
			RefreshRecipeRows();
			RefreshReasons();
			RaiseStateChanged();
		}
	}

	private void Redo()
	{
		var snapshot = _domainFacade.Redo();
		if (snapshot is not null)
		{
			RefreshRecipeRows();
			RefreshReasons();
			RaiseStateChanged();
		}
	}

	private void RefreshRecipeRows()
	{
		var recipe = _domainFacade.CurrentRecipe;

		// Dispose old rows to unsubscribe event handlers
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

	private void RefreshReasons()
	{
		// Remove previous structural reason entries
		for (var i = LogEntries.Count - 1; i >= 0; i--)
		{
			if (LogEntries[i].IsStructural)
			{
				LogEntries.RemoveAt(i);
			}
		}

		// Add current snapshot reasons
		var snapshot = _domainFacade.Snapshot;
		foreach (var reason in snapshot.Reasons)
		{
			var severity = reason is AbstractError ? LogSeverity.Error : LogSeverity.Warning;
			var entry = new LogEntry(severity, reason.Message, LogEntry.StructuralSource, DateTime.Now);
			LogEntries.Add(entry);
		}
	}

	private void ClearLog()
	{
		// Remove only non-structural entries; structural ones are managed by RefreshReasons
		for (var i = LogEntries.Count - 1; i >= 0; i--)
		{
			if (!LogEntries[i].IsStructural)
			{
				LogEntries.RemoveAt(i);
			}
		}
	}

	private void ToggleLogPanel()
	{
		IsLogPanelVisible = !IsLogPanelVisible;
	}

	private static void Exit()
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			lifetime.Shutdown();
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

			// After domain mutation, update the row VM's step reference
			// so GetPropertyValue reads from the latest immutable step
			var updatedStep = _domainFacade.CurrentRecipe.Steps[stepIndex];
			RecipeRows[stepIndex].UpdateStep(updatedStep);
		}
		catch (Exception ex)
		{
			_notificationService.ShowError($"Step {stepIndex + 1}: {ex.Message}");
		}

		RefreshReasons();
		RaiseStateChanged();
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
			_notificationService.ShowError($"Step {stepIndex + 1}: Failed to change action - {ex.Message}");
		}

		RefreshReasons();
		RaiseStateChanged();
	}

	private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RaiseLogStateChanged();
	}

	private void RaiseLogStateChanged()
	{
		this.RaisePropertyChanged(nameof(HasLogEntries));
		this.RaisePropertyChanged(nameof(ShowLogPanel));
		this.RaisePropertyChanged(nameof(HasErrors));
		this.RaisePropertyChanged(nameof(HasWarnings));
		this.RaisePropertyChanged(nameof(ErrorCount));
		this.RaisePropertyChanged(nameof(WarningCount));
		this.RaisePropertyChanged(nameof(ErrorCountText));
		this.RaisePropertyChanged(nameof(WarningCountText));
		this.RaisePropertyChanged(nameof(StatusErrorSummary));
		this.RaisePropertyChanged(nameof(HasStatusErrors));
	}

	private void RaiseStateChanged()
	{
		this.RaisePropertyChanged(nameof(IsDirty));
		this.RaisePropertyChanged(nameof(StatusText));
		this.RaisePropertyChanged(nameof(CanUndo));
		this.RaisePropertyChanged(nameof(CanRedo));
		this.RaisePropertyChanged(nameof(CanDeleteStep));
		this.RaisePropertyChanged(nameof(WindowTitle));
	}
}
