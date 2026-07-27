using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeFile;

public class RecipeFileViewModel : ReactiveObject, IDisposable
{
	private readonly RecipeCoordinator _coordinator;

	private readonly CompositeDisposable _disposables = new();
	private readonly ILogger<RecipeFileViewModel> _logger;
	private readonly MessagePanelViewModel _messagePanel;

	public RecipeFileViewModel(
		RecipeCoordinator coordinator,
		MessagePanelViewModel messagePanel,
		ILogger<RecipeFileViewModel> logger)
	{
		_coordinator = coordinator;
		_messagePanel = messagePanel;
		_logger = logger;

		OpenFileInteraction = new Interaction<Unit, string?>();
		SaveFileInteraction = new Interaction<string?, string?>();

		var canEdit = _coordinator.CanEditRecipe;

		SaveRecipeCommand = ReactiveCommand.CreateFromTask(SaveRecipeAsync);
		SaveAsRecipeCommand = ReactiveCommand.CreateFromTask(SaveAsRecipeAsync);
		LoadRecipeCommand = ReactiveCommand.CreateFromTask(LoadRecipeAsync, canEdit);
		NewRecipeCommand = ReactiveCommand.Create(NewRecipe, canEdit);

		SaveRecipeCommand.ReportThrownExceptions(_messagePanel, _logger, "Save failed")
			.DisposeWith(_disposables);

		SaveAsRecipeCommand.ReportThrownExceptions(_messagePanel, _logger, "Save As failed")
			.DisposeWith(_disposables);

		LoadRecipeCommand.ReportThrownExceptions(_messagePanel, _logger, "Load failed")
			.DisposeWith(_disposables);
	}

	public Interaction<Unit, string?> OpenFileInteraction { get; }

	public Interaction<string?, string?> SaveFileInteraction { get; }

	public ReactiveCommand<Unit, bool> SaveRecipeCommand { get; }

	public ReactiveCommand<Unit, bool> SaveAsRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> LoadRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> NewRecipeCommand { get; }

	public string? CurrentFilePath { get; private set; }

	public void Dispose()
	{
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task<bool> SaveRecipeAsync()
	{
		if (CurrentFilePath is not null)
		{
			return await SaveToFileAsync(CurrentFilePath);
		}

		return await SaveAsRecipeAsync();
	}

	private async Task<bool> SaveAsRecipeAsync()
	{
		var suggestedName = CurrentFilePath is not null
			? Path.GetFileNameWithoutExtension(CurrentFilePath)
			: null;

		var filePath = await SaveFileInteraction.Handle(suggestedName);
		if (filePath is null)
		{
			return false;
		}

		return await SaveToFileAsync(filePath);
	}

	private async Task<bool> SaveToFileAsync(string filePath)
	{
		var result = await _coordinator.SaveRecipeAsync(filePath);

		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result, "Failed to save recipe");

			return false;
		}

		CurrentFilePath = filePath;
		_messagePanel.ReportSuccess($"Saved: {Path.GetFileName(filePath)}");

		return true;
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
			_messagePanel.ReportFailure(result);

			return;
		}

		CurrentFilePath = filePath;
		_messagePanel.ReportSuccess($"Loaded: {Path.GetFileName(filePath)}");
	}

	private void NewRecipe()
	{
		var result = _coordinator.NewRecipe();

		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result);

			return;
		}

		CurrentFilePath = null;
	}
}
