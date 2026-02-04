using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using Domain.Facade;

using ReactiveUI;

using Shared;
using Shared.Registries;

namespace UI.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
	private readonly DomainFacade _domainFacade;
	private readonly IActionRegistry _actionRegistry;
	private readonly IGroupRegistry _groupRegistry;
	private readonly IColumnRegistry _columnRegistry;

	public MainWindowViewModel(
		DomainFacade domainFacade,
		IActionRegistry actionRegistry,
		IGroupRegistry groupRegistry,
		IColumnRegistry columnRegistry)
	{
		_domainFacade = domainFacade;
		_actionRegistry = actionRegistry;
		_groupRegistry = groupRegistry;
		_columnRegistry = columnRegistry;

		RecipeRows = new ObservableCollection<RecipeRowViewModel>();
		ValidationErrors = new ObservableCollection<string>();
		ValidationErrors.CollectionChanged += OnValidationErrorsChanged;

		DeleteStepCommand = ReactiveCommand.Create<int>(DeleteStep);
		SaveRecipeCommand = ReactiveCommand.Create(SaveRecipe);
		LoadRecipeCommand = ReactiveCommand.Create(LoadRecipe);
		NewRecipeCommand = ReactiveCommand.Create(NewRecipe);
		UndoCommand = ReactiveCommand.Create(Undo);
		RedoCommand = ReactiveCommand.Create(Redo);
		ExitCommand = ReactiveCommand.Create(Exit);
	}

	public IActionRegistry ActionRegistry => _actionRegistry;

	public IGroupRegistry GroupRegistry => _groupRegistry;

	public ObservableCollection<RecipeRowViewModel> RecipeRows { get; }

	public ObservableCollection<string> ValidationErrors { get; }

	public AppConfiguration? Configuration { get; private set; }

	public ReactiveCommand<int, Unit> DeleteStepCommand { get; }

	public ReactiveCommand<Unit, Unit> SaveRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> LoadRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> NewRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> UndoCommand { get; }

	public ReactiveCommand<Unit, Unit> RedoCommand { get; }

	public ReactiveCommand<Unit, Unit> ExitCommand { get; }

	public string WindowTitle => "SemiStep - Core Editor";

	public bool IsDirty => _domainFacade.IsDirty;

	public bool CanUndo => _domainFacade.CanUndo;

	public bool CanRedo => _domainFacade.CanRedo;

	public bool IsConnectedToPlc => false;

	public string StatusText => IsDirty ? "Modified" : "Saved";

	public string ConnectionStatus => IsConnectedToPlc ? "Connected" : "Disconnected";

	public bool HasValidationErrors => ValidationErrors.Count > 0;

	public void Initialize(AppConfiguration configuration)
	{
		Configuration = configuration;
		RefreshRecipeRows();
	}

	private void DeleteStep(int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= RecipeRows.Count)
		{
			return;
		}

		_domainFacade.RemoveStep(stepIndex);
		RefreshRecipeRows();
		RaiseStateChanged();
	}

	private void SaveRecipe()
	{
		_domainFacade.SaveRecipe();
		RaiseStateChanged();
	}

	private void LoadRecipe()
	{
		_domainFacade.LoadRecipe();
		RefreshRecipeRows();
		RaiseStateChanged();
	}

	private void NewRecipe()
	{
		_domainFacade.NewRecipe();
		RefreshRecipeRows();
		RaiseStateChanged();
	}

	private void Undo()
	{
		var snapshot = _domainFacade.Undo();
		if (snapshot is not null)
		{
			RefreshRecipeRows();
			RaiseStateChanged();
		}
	}

	private void Redo()
	{
		var snapshot = _domainFacade.Redo();
		if (snapshot is not null)
		{
			RefreshRecipeRows();
			RaiseStateChanged();
		}
	}

	private void RefreshRecipeRows()
	{
		var recipe = _domainFacade.CurrentRecipe;
		RecipeRows.Clear();
		ValidationErrors.Clear();

		for (var i = 0; i < recipe.StepCount; i++)
		{
			var step = recipe.Steps[i];
			var action = _actionRegistry.GetAction(step.ActionKey);
			var rowVm = new RecipeRowViewModel(
				i + 1,
				step,
				action,
				_groupRegistry,
				_columnRegistry,
				OnCellValueChanged,
				OnActionChanged);
			RecipeRows.Add(rowVm);
		}
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
		}
		catch (Exception ex)
		{
			ValidationErrors.Add($"Step {stepIndex + 1}: {ex.Message}");
		}

		RefreshRecipeRows();
		RaiseStateChanged();
	}

	private void OnActionChanged(int stepIndex, int newActionId)
	{
		try
		{
			_domainFacade.ChangeStepAction(stepIndex, newActionId);
			RefreshRecipeRows();
			RaiseStateChanged();
		}
		catch (Exception ex)
		{
			ValidationErrors.Add($"Step {stepIndex + 1}: Failed to change action - {ex.Message}");
		}
	}

	private void OnValidationErrorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		this.RaisePropertyChanged(nameof(HasValidationErrors));
	}

	private void RaiseStateChanged()
	{
		this.RaisePropertyChanged(nameof(IsDirty));
		this.RaisePropertyChanged(nameof(StatusText));
		this.RaisePropertyChanged(nameof(CanUndo));
		this.RaisePropertyChanged(nameof(CanRedo));
	}
}
