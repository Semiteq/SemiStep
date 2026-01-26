using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;

using ReactiveUI;

using Domain.Facade;

using Recipe.Exceptions;

using Shared;
using Shared.Registries;

namespace UI.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
	private readonly DomainFacade _domainFacade;
	private readonly IActionRegistry _actionRegistry;
	private readonly IPropertyRegistry _propertyRegistry;

	public MainWindowViewModel(
		DomainFacade domainFacade,
		IActionRegistry actionRegistry,
		IPropertyRegistry propertyRegistry)
	{
		_domainFacade = domainFacade;
		_actionRegistry = actionRegistry;
		_propertyRegistry = propertyRegistry;

		RecipeRows = new ObservableCollection<RecipeRowViewModel>();
		ValidationErrors = new ObservableCollection<string>();
		ValidationErrors.CollectionChanged += OnValidationErrorsChanged;

		AddStepCommand = ReactiveCommand.Create(AddStep);
		DeleteStepCommand = ReactiveCommand.Create<int>(DeleteStep);
		SaveRecipeCommand = ReactiveCommand.Create(SaveRecipe);
		LoadRecipeCommand = ReactiveCommand.Create(LoadRecipe);
		NewRecipeCommand = ReactiveCommand.Create(NewRecipe);
	}

	public ObservableCollection<RecipeRowViewModel> RecipeRows { get; }

	public ObservableCollection<string> ValidationErrors { get; }

	public AppConfiguration? Configuration { get; private set; }

	public ReactiveCommand<Unit, Unit> AddStepCommand { get; }

	public ReactiveCommand<int, Unit> DeleteStepCommand { get; }

	public ReactiveCommand<Unit, Unit> SaveRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> LoadRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> NewRecipeCommand { get; }

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
				_propertyRegistry,
				OnCellValueChanged);
			RecipeRows.Add(rowVm);
		}
	}

	private void OnCellValueChanged(int stepIndex, string propertyTypeId, object? value)
	{
		if (value is null)
		{
			return;
		}

		try
		{
			_domainFacade.Recipe.UpdateProperty(stepIndex, propertyTypeId, value);
			RefreshRecipeRows();
			this.RaisePropertyChanged(nameof(IsDirty));
			this.RaisePropertyChanged(nameof(StatusText));
		}
		catch (PropertyValidationException ex)
		{
			ValidationErrors.Add($"Step {stepIndex + 1}: {ex.Message}");
		}
	}

	private void OnValidationErrorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		this.RaisePropertyChanged(nameof(HasValidationErrors));
	}
}
