using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using Domain.Facade;

using ReactiveUI;

using UI.Services;

namespace UI.ViewModels;

public class RecipeFileViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly DomainFacade _domainFacade;
	private readonly LogPanelViewModel _logPanel;
	private readonly Action _notifyStateChanged;
	private readonly INotificationService _notificationService;
	private readonly RecipeGridViewModel _recipeGrid;

	public RecipeFileViewModel(
		DomainFacade domainFacade,
		RecipeGridViewModel recipeGrid,
		LogPanelViewModel logPanel,
		INotificationService notificationService,
		Action notifyStateChanged)
	{
		_domainFacade = domainFacade;
		_recipeGrid = recipeGrid;
		_logPanel = logPanel;
		_notificationService = notificationService;
		_notifyStateChanged = notifyStateChanged;

		OpenFileInteraction = new Interaction<Unit, string?>();
		SaveFileInteraction = new Interaction<string?, string?>();

		SaveRecipeCommand = ReactiveCommand.CreateFromTask(SaveRecipeAsync);
		SaveAsRecipeCommand = ReactiveCommand.CreateFromTask(SaveAsRecipeAsync);
		LoadRecipeCommand = ReactiveCommand.CreateFromTask(LoadRecipeAsync);
		NewRecipeCommand = ReactiveCommand.Create(NewRecipe);

		SaveRecipeCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _notificationService.ShowError($"Save failed: {ex.Message}"))
			.DisposeWith(_disposables);

		SaveAsRecipeCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _notificationService.ShowError($"Save As failed: {ex.Message}"))
			.DisposeWith(_disposables);

		LoadRecipeCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _notificationService.ShowError($"Load failed: {ex.Message}"))
			.DisposeWith(_disposables);
	}

	public Interaction<Unit, string?> OpenFileInteraction { get; }

	public Interaction<string?, string?> SaveFileInteraction { get; }

	public ReactiveCommand<Unit, Unit> SaveRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> SaveAsRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> LoadRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> NewRecipeCommand { get; }

	public string? CurrentFilePath { get; private set; }

	public void Dispose()
	{
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task SaveRecipeAsync()
	{
		if (CurrentFilePath is not null)
		{
			await SaveToFileAsync(CurrentFilePath);

			return;
		}

		await SaveAsRecipeAsync();
	}

	private async Task SaveAsRecipeAsync()
	{
		var suggestedName = CurrentFilePath is not null
			? Path.GetFileNameWithoutExtension(CurrentFilePath)
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
			CurrentFilePath = filePath;
			_notifyStateChanged();
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
			var result = await _domainFacade.LoadRecipeAsync(filePath);
			if (result.IsFailed)
			{
				var errorMessages = string.Join(
					Environment.NewLine,
					result.Errors.Select(e => e.Message));
				_notificationService.ShowError(
					$"Failed to load recipe:{Environment.NewLine}{errorMessages}");

				return;
			}

			CurrentFilePath = filePath;
			_recipeGrid.RebuildAllRows(_domainFacade.CurrentRecipe);
			_recipeGrid.RefreshAfterMutation();
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
		CurrentFilePath = null;

		_logPanel.Clear();
		_recipeGrid.RebuildAllRows(_domainFacade.CurrentRecipe);
		_recipeGrid.RefreshAfterMutation();
	}
}
