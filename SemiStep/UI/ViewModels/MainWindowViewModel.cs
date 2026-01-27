using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using Domain.Facade;
using Domain.Services;

using ReactiveUI;

using Recipe.Exceptions;

using Shared;
using Shared.Registries;
using Shared.Services;

namespace UI.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
	private readonly DomainFacade _domainFacade;
	private readonly IActionRegistry _actionRegistry;
	private readonly IGroupRegistry _groupRegistry;
	private readonly IColumnRegistry _columnRegistry;
	private readonly CellStateResolver _cellStateResolver;
	private readonly IGridStyleProvider _gridStyleProvider;

	public MainWindowViewModel(
		DomainFacade domainFacade,
		IActionRegistry actionRegistry,
		IGroupRegistry groupRegistry,
		IColumnRegistry columnRegistry,
		CellStateResolver cellStateResolver,
		IGridStyleProvider gridStyleProvider)
	{
		_domainFacade = domainFacade;
		_actionRegistry = actionRegistry;
		_groupRegistry = groupRegistry;
		_columnRegistry = columnRegistry;
		_cellStateResolver = cellStateResolver;
		_gridStyleProvider = gridStyleProvider;

		RecipeRows = new ObservableCollection<RecipeRowViewModel>();
		ValidationErrors = new ObservableCollection<string>();
		ValidationErrors.CollectionChanged += OnValidationErrorsChanged;

		AddStepCommand = ReactiveCommand.Create(AddStep);
		DeleteStepCommand = ReactiveCommand.Create<int>(DeleteStep);
		SaveRecipeCommand = ReactiveCommand.Create(SaveRecipe);
		LoadRecipeCommand = ReactiveCommand.Create(LoadRecipe);
		NewRecipeCommand = ReactiveCommand.Create(NewRecipe);
		ExitCommand = ReactiveCommand.Create(Exit);
	}

	public IActionRegistry ActionRegistry => _actionRegistry;

	public IGroupRegistry GroupRegistry => _groupRegistry;

	public IColumnRegistry ColumnRegistry => _columnRegistry;

	public CellStateResolver CellStateResolver => _cellStateResolver;

	public IGridStyleProvider GridStyleProvider => _gridStyleProvider;

	public ObservableCollection<RecipeRowViewModel> RecipeRows { get; }

	public ObservableCollection<string> ValidationErrors { get; }

	public AppConfiguration? Configuration { get; private set; }

	public ReactiveCommand<Unit, Unit> AddStepCommand { get; }

	public ReactiveCommand<int, Unit> DeleteStepCommand { get; }

	public ReactiveCommand<Unit, Unit> SaveRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> LoadRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> NewRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> ExitCommand { get; }

	public string WindowTitle => "SemiStep - Recipe Editor";

	public bool IsDirty => _domainFacade.Recipe.IsDirty;

	public bool IsConnectedToPlc => false;

	public string StatusText => IsDirty ? "Modified" : "Saved";

	public string ConnectionStatus => IsConnectedToPlc ? "Connected" : "Disconnected";

	public bool HasValidationErrors => ValidationErrors.Count > 0;

	public void Initialize(AppConfiguration configuration)
	{
		Configuration = configuration;
		RefreshRecipeRows();
	}

	private void AddStep()
	{
		var firstAction = _actionRegistry.GetAll().FirstOrDefault();
		if (firstAction is null)
		{
			return;
		}

		_domainFacade.Recipe.AddStep(firstAction.Id);
		RefreshRecipeRows();
		this.RaisePropertyChanged(nameof(IsDirty));
		this.RaisePropertyChanged(nameof(StatusText));
	}

	private void DeleteStep(int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= RecipeRows.Count)
		{
			return;
		}

		_domainFacade.Recipe.RemoveStep(stepIndex);
		RefreshRecipeRows();
		this.RaisePropertyChanged(nameof(IsDirty));
		this.RaisePropertyChanged(nameof(StatusText));
	}

	private void SaveRecipe()
	{
		_domainFacade.Recipe.MarkSaved();
		this.RaisePropertyChanged(nameof(IsDirty));
		this.RaisePropertyChanged(nameof(StatusText));
	}

	private void LoadRecipe()
	{
		_domainFacade.Recipe.NewRecipe();
		RefreshRecipeRows();
		this.RaisePropertyChanged(nameof(IsDirty));
		this.RaisePropertyChanged(nameof(StatusText));
	}

	private void NewRecipe()
	{
		_domainFacade.Recipe.NewRecipe();
		RefreshRecipeRows();
		this.RaisePropertyChanged(nameof(IsDirty));
		this.RaisePropertyChanged(nameof(StatusText));
	}

	private void RefreshRecipeRows()
	{
		var recipe = _domainFacade.Recipe.CurrentRecipe;
		RecipeRows.Clear();
		ValidationErrors.Clear();

		for (var i = 0; i < recipe.StepCount; i++)
		{
			var step = recipe.Steps[i];
			var action = _actionRegistry.GetAction(short.Parse(step.ActionKey));
			var rowVm = new RecipeRowViewModel(
				i + 1,
				step,
				action,
				_groupRegistry,
				_columnRegistry,
				_cellStateResolver,
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
			_domainFacade.Recipe.UpdateProperty(stepIndex, columnKey, value);
			RefreshRecipeRows();
			this.RaisePropertyChanged(nameof(IsDirty));
			this.RaisePropertyChanged(nameof(StatusText));
		}
		catch (PropertyValidationException ex)
		{
			ValidationErrors.Add($"Step {stepIndex + 1}: {ex.Message}");
		}
	}

	private void OnActionChanged(int stepIndex, short newActionId)
	{
		try
		{
			_domainFacade.Recipe.ChangeStepAction(stepIndex, newActionId);
			RefreshRecipeRows();
			this.RaisePropertyChanged(nameof(IsDirty));
			this.RaisePropertyChanged(nameof(StatusText));
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
}
