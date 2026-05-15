using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using ReactiveUI;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeFile;

public class RecipeFileViewModel : ReactiveObject, IDisposable
{
	private const string FileSource = "File";
	private readonly RecipeMutationCoordinator _coordinator;

	private readonly CompositeDisposable _disposables = new();
	private readonly MessagePanelViewModel _messagePanel;

	public RecipeFileViewModel(
		RecipeMutationCoordinator coordinator,
		MessagePanelViewModel messagePanel)
	{
		_coordinator = coordinator;
		_messagePanel = messagePanel;

		OpenFileInteraction = new Interaction<Unit, string?>();
		SaveFileInteraction = new Interaction<string?, string?>();

		SaveRecipeCommand = ReactiveCommand.CreateFromTask(SaveRecipeAsync);
		SaveAsRecipeCommand = ReactiveCommand.CreateFromTask(SaveAsRecipeAsync);
		LoadRecipeCommand = ReactiveCommand.CreateFromTask(LoadRecipeAsync);
		NewRecipeCommand = ReactiveCommand.Create(NewRecipe);

		SaveRecipeCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(ex => _messagePanel.AddError($"Save failed: {ex.Message}", FileSource))
			.DisposeWith(_disposables);

		SaveAsRecipeCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(ex => _messagePanel.AddError($"Save As failed: {ex.Message}", FileSource))
			.DisposeWith(_disposables);

		LoadRecipeCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(ex => _messagePanel.AddError($"Load failed: {ex.Message}", FileSource))
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
		var result = await _coordinator.SaveRecipeAsync(filePath);

		if (result.IsFailed)
		{
			_messagePanel.AddError($"Failed to save recipe: {result.Errors[0].Message}", FileSource);

			return;
		}

		CurrentFilePath = filePath;
		_messagePanel.AddInfo($"Saved: {Path.GetFileName(filePath)}", FileSource);
	}

	private async Task LoadRecipeAsync()
	{
		var filePath = await OpenFileInteraction.Handle(Unit.Default);
		if (filePath is null)
		{
			return;
		}

		var result = await _coordinator.LoadRecipeAsync(filePath);
		if (result.IsFailed)
		{
			_messagePanel.AddError(result.Errors[0].Message, FileSource);

			return;
		}

		CurrentFilePath = filePath;
		_messagePanel.AddInfo($"Loaded: {Path.GetFileName(filePath)}", FileSource);
	}

	private void NewRecipe()
	{
		var result = _coordinator.NewRecipe();

		if (result.IsFailed)
		{
			_messagePanel.AddError(result.Errors[0].Message, FileSource);

			return;
		}

		CurrentFilePath = null;
	}
}
