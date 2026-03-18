using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using Avalonia.Input.Platform;

using Domain.Facade;

using ReactiveUI;

using UI.Services;

namespace UI.ViewModels;

public class ClipboardViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly DomainFacade _domainFacade;
	private readonly INotificationService _notificationService;
	private readonly RecipeGridViewModel _recipeGrid;
	private IClipboard? _clipboard;

	public ClipboardViewModel(
		DomainFacade domainFacade,
		RecipeGridViewModel recipeGrid,
		INotificationService notificationService)
	{
		_domainFacade = domainFacade;
		_recipeGrid = recipeGrid;
		_notificationService = notificationService;

		var canCopyOrCut = _recipeGrid.WhenAnyValue(x => x.CanDeleteStep);

		CopyStepCommand = ReactiveCommand.CreateFromTask(CopyStepsAsync, canCopyOrCut);
		CutStepCommand = ReactiveCommand.CreateFromTask(CutStepsAsync, canCopyOrCut);
		PasteStepCommand = ReactiveCommand.CreateFromTask(PasteStepsAsync);

		CopyStepCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _notificationService.ShowError($"Copy failed: {ex.Message}"))
			.DisposeWith(_disposables);

		CutStepCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _notificationService.ShowError($"Cut failed: {ex.Message}"))
			.DisposeWith(_disposables);

		PasteStepCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _notificationService.ShowError($"Paste failed: {ex.Message}"))
			.DisposeWith(_disposables);
	}

	public ReactiveCommand<Unit, Unit> CopyStepCommand { get; }

	public ReactiveCommand<Unit, Unit> CutStepCommand { get; }

	public ReactiveCommand<Unit, Unit> PasteStepCommand { get; }

	public void SetClipboard(IClipboard? clipboard)
	{
		_clipboard = clipboard;
	}

	public void Dispose()
	{
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task CopyStepsAsync()
	{
		if (_clipboard is null || _recipeGrid.SelectedRowIndices.Count == 0)
		{
			return;
		}

		var steps = _recipeGrid.CollectSelectedSteps();
		var csvText = _domainFacade.SerializeStepsForClipboard(steps);
		await _clipboard.SetTextAsync(csvText);
	}

	private async Task CutStepsAsync()
	{
		if (_clipboard is null || _recipeGrid.SelectedRowIndices.Count == 0)
		{
			return;
		}

		var steps = _recipeGrid.CollectSelectedSteps();
		var csvText = _domainFacade.SerializeStepsForClipboard(steps);
		await _clipboard.SetTextAsync(csvText);

		_recipeGrid.DeleteStep();
	}

	private async Task PasteStepsAsync()
	{
		if (_clipboard is null)
		{
			return;
		}

		var csvText = await _clipboard.GetTextAsync();
		if (string.IsNullOrWhiteSpace(csvText))
		{
			return;
		}

		var recipeResult = _domainFacade.DeserializeStepsFromClipboard(csvText);
		if (recipeResult.IsFailed)
		{
			var errorMessages = string.Join(
				Environment.NewLine,
				recipeResult.Errors.Select(e => e.Message));
			_notificationService.ShowError($"Paste failed:{Environment.NewLine}{errorMessages}");

			return;
		}

		var insertIndex = _recipeGrid.SelectedRowIndices.Count > 0
			? _recipeGrid.SelectedRowIndices.Max() + 1
			: _recipeGrid.RecipeRows.Count;

		_domainFacade.InsertSteps(insertIndex, recipeResult.Value.Steps);
		_recipeGrid.InsertRowsForSteps(insertIndex, recipeResult.Value.Steps.Count);
		_recipeGrid.RefreshAfterMutation();
		_recipeGrid.SelectedRowIndex = insertIndex;
	}
}
