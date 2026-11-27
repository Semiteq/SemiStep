using System.Globalization;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.Core.Shared;

using SemiStep.UI.Coordinator;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeFile;

public class RecipeFileViewModel : ReactiveObject, IDisposable
{
	private readonly RecipeCoordinator _coordinator;

	private readonly CompositeDisposable _disposables = [];
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
		UnsavedChangesInteraction = new Interaction<Unit, UnsavedChangesChoice>();

		var canEdit = _coordinator.CanEditRecipe;

		SaveRecipeCommand = ReactiveCommand.CreateFromTask(SaveRecipeAsync);
		SaveAsRecipeCommand = ReactiveCommand.CreateFromTask(SaveAsRecipeAsync);
		LoadRecipeCommand = ReactiveCommand.CreateFromTask(LoadRecipeAsync, canEdit);
		NewRecipeCommand = ReactiveCommand.CreateFromTask(NewRecipeAsync, canEdit);

		SaveRecipeCommand.ReportThrownExceptions(_messagePanel, _logger, new LocalizedText(nameof(Resources.SaveFailed)))
			.DisposeWith(_disposables);

		SaveAsRecipeCommand.ReportThrownExceptions(_messagePanel, _logger, new LocalizedText(nameof(Resources.SaveAsFailed)))
			.DisposeWith(_disposables);

		LoadRecipeCommand.ReportThrownExceptions(_messagePanel, _logger, new LocalizedText(nameof(Resources.LoadFailed)))
			.DisposeWith(_disposables);

		NewRecipeCommand.ReportThrownExceptions(_messagePanel, _logger, new LocalizedText(nameof(Resources.NewRecipeFailed)))
			.DisposeWith(_disposables);
	}

	public Interaction<Unit, string?> OpenFileInteraction { get; }

	public Interaction<string?, string?> SaveFileInteraction { get; }

	public Interaction<Unit, UnsavedChangesChoice> UnsavedChangesInteraction { get; }

	public ReactiveCommand<Unit, bool> SaveRecipeCommand { get; }

	public ReactiveCommand<Unit, bool> SaveAsRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> LoadRecipeCommand { get; }

	public ReactiveCommand<Unit, Unit> NewRecipeCommand { get; }

	public string? CurrentFilePath
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	public void Dispose()
	{
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	/// <summary>Applies the answer to the unsaved-changes dialog; true when the caller may drop the recipe.</summary>
	public async Task<bool> ResolveUnsavedChangesAsync(UnsavedChangesChoice choice)
	{
		return choice switch
		{
			UnsavedChangesChoice.Save => await SaveBeforeDiscardAsync(),
			UnsavedChangesChoice.DontSave => true,
			_ => false
		};
	}

	private async Task<bool> ConfirmDiscardAsync()
	{
		if (!_coordinator.IsDirty)
		{
			return true;
		}

		var choice = await UnsavedChangesInteraction.Handle(Unit.Default);

		return await ResolveUnsavedChangesAsync(choice);
	}

	private async Task<bool> SaveBeforeDiscardAsync()
	{
		var saveAlreadyInFlight = await SaveRecipeCommand.IsExecuting.FirstAsync();
		if (saveAlreadyInFlight)
		{
			return false;
		}

		try
		{
			return await SaveRecipeCommand.Execute();
		}
		catch (Exception)
		{
			// The command's ThrownExceptions pipeline already reports the failure.
			return false;
		}
	}

	private async Task<bool> SaveRecipeAsync()
	{
		return CurrentFilePath is not null ? await SaveToFileAsync(CurrentFilePath) : await SaveAsRecipeAsync();
	}

	private async Task<bool> SaveAsRecipeAsync()
	{
		var suggestedName = CurrentFilePath is not null
			? Path.GetFileNameWithoutExtension(CurrentFilePath)
			: null;

		var filePath = await SaveFileInteraction.Handle(suggestedName);
		return filePath is not null && await SaveToFileAsync(filePath);
	}

	private async Task<bool> SaveToFileAsync(string filePath)
	{
		var result = await _coordinator.SaveRecipeAsync(filePath);

		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result, Resources.SaveRecipeFailed);

			return false;
		}

		CurrentFilePath = filePath;
		_messagePanel.ReportSuccess(
			string.Format(CultureInfo.InvariantCulture, Resources.SavedFormat, Path.GetFileName(filePath)));

		return true;
	}

	private async Task LoadRecipeAsync()
	{
		if (!await ConfirmDiscardAsync())
		{
			return;
		}

		var filePath = await OpenFileInteraction.Handle(Unit.Default);
		if (filePath is null || _coordinator.IsRecipeActive)
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

		if (result.Successes.OfType<Warning>().Any())
		{
			_messagePanel.ReportWarnings(result);
		}
		else
		{
			_messagePanel.ReportSuccess(
				string.Format(CultureInfo.InvariantCulture, Resources.LoadedFormat, Path.GetFileName(filePath)));
		}
	}

	private async Task NewRecipeAsync()
	{
		if (!await ConfirmDiscardAsync() || _coordinator.IsRecipeActive)
		{
			return;
		}

		var result = _coordinator.NewRecipe();

		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result);

			return;
		}

		CurrentFilePath = null;
	}
}
