using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using Avalonia.Input.Platform;

using ReactiveUI;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeGrid;

namespace SemiStep.UI.Clipboard;

public class ClipboardViewModel : ReactiveObject, IDisposable
{
	private const string ClipboardSource = "Clipboard";
	private readonly RecipeMutationCoordinator _coordinator;

	private readonly CompositeDisposable _disposables = new();
	private readonly MessagePanelViewModel _messagePanel;
	private readonly RecipeGridViewModel _recipeGrid;
	private IClipboard? _clipboard;

	public ClipboardViewModel(
		RecipeMutationCoordinator coordinator,
		RecipeGridViewModel recipeGrid,
		MessagePanelViewModel messagePanel)
	{
		_coordinator = coordinator;
		_recipeGrid = recipeGrid;
		_messagePanel = messagePanel;

		var canCopyOrCut = _recipeGrid.WhenAnyValue(x => x.CanDeleteStep);

		CopyStepCommand = ReactiveCommand.CreateFromTask(CopyStepsAsync, canCopyOrCut);
		CutStepCommand = ReactiveCommand.CreateFromTask(CutStepsAsync, canCopyOrCut);
		PasteStepCommand = ReactiveCommand.CreateFromTask(PasteStepsAsync);

		CopyStepCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _messagePanel.AddError($"Copy failed: {ex.Message}", ClipboardSource))
			.DisposeWith(_disposables);

		CutStepCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _messagePanel.AddError($"Cut failed: {ex.Message}", ClipboardSource))
			.DisposeWith(_disposables);

		PasteStepCommand.ThrownExceptions
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(ex => _messagePanel.AddError($"Paste failed: {ex.Message}", ClipboardSource))
			.DisposeWith(_disposables);
	}

	public ReactiveCommand<Unit, Unit> CopyStepCommand { get; }

	public ReactiveCommand<Unit, Unit> CutStepCommand { get; }

	public ReactiveCommand<Unit, Unit> PasteStepCommand { get; }

	public void Dispose()
	{
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	public void SetClipboard(IClipboard? clipboard)
	{
		_clipboard = clipboard;
	}

	private async Task CopyStepsAsync()
	{
		if (_clipboard is null || _recipeGrid.SelectedRowIndices.Count == 0)
		{
			return;
		}

		var steps = _recipeGrid.CollectSelectedSteps();
		var csvText = _coordinator.QueryService.SerializeStepsForClipboard(steps);
		await _clipboard.SetTextAsync(csvText);
	}

	private async Task CutStepsAsync()
	{
		if (_clipboard is null || _recipeGrid.SelectedRowIndices.Count == 0)
		{
			return;
		}

		var steps = _recipeGrid.CollectSelectedSteps();
		var csvText = _coordinator.QueryService.SerializeStepsForClipboard(steps);
		await _clipboard.SetTextAsync(csvText);

		var result = _coordinator.RemoveSteps(_recipeGrid.SelectedRowIndices);
		if (result.IsFailed)
		{
			_messagePanel.AddError(result.Errors[0].Message, ClipboardSource);
		}
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

		var recipeResult = _coordinator.QueryService.DeserializeStepsFromClipboard(csvText);
		if (recipeResult.IsFailed)
		{
			var errorMessages = string.Join(
				Environment.NewLine,
				recipeResult.Errors.Select(e => e.Message));

			_messagePanel.AddError($"Paste failed: {errorMessages}", ClipboardSource);

			return;
		}

		var insertIndex = _recipeGrid.SelectedRowIndices.Count > 0
			? _recipeGrid.SelectedRowIndices.Max() + 1
			: _recipeGrid.RecipeRows.Count;

		var insertResult = _coordinator.InsertSteps(insertIndex, recipeResult.Value.Steps);
		if (insertResult.IsFailed)
		{
			_messagePanel.AddError(insertResult.Errors[0].Message, ClipboardSource);
		}
	}
}
