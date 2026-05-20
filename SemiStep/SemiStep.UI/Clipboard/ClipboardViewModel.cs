using System.Collections.Immutable;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Input.Platform;

using FluentResults;

using ReactiveUI;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeGrid;

namespace SemiStep.UI.Clipboard;

public class ClipboardViewModel : ReactiveObject, IDisposable
{
	private const string ClipboardSource = "Clipboard";
	private readonly ClipboardSerializer _clipboardSerializer;
	private readonly RecipeCoordinator _coordinator;

	private readonly CompositeDisposable _disposables = new();
	private readonly ImportedRecipeValidator _importedRecipeValidator;
	private readonly MessagePanelViewModel _messagePanel;
	private readonly RecipeGridViewModel _recipeGrid;
	private IClipboard? _clipboard;

	public ClipboardViewModel(
		RecipeCoordinator coordinator,
		RecipeGridViewModel recipeGrid,
		ClipboardSerializer clipboardSerializer,
		ImportedRecipeValidator importedRecipeValidator,
		MessagePanelViewModel messagePanel)
	{
		_coordinator = coordinator;
		_recipeGrid = recipeGrid;
		_clipboardSerializer = clipboardSerializer;
		_importedRecipeValidator = importedRecipeValidator;
		_messagePanel = messagePanel;

		var canCopyOrCut = _recipeGrid.WhenAnyValue(x => x.CanDeleteStep);
		var canEdit = _coordinator.CanEditRecipe;

		CopyStepCommand = ReactiveCommand.CreateFromTask(CopyStepsAsync, canCopyOrCut);
		CutStepCommand = ReactiveCommand.CreateFromTask(
			CutStepsAsync,
			canEdit.CombineLatest(canCopyOrCut, (left, right) => left && right));
		PasteStepCommand = ReactiveCommand.CreateFromTask(PasteStepsAsync, canEdit);

		CopyStepCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(ex => _messagePanel.AddError($"Copy failed: {ex.Message}", ClipboardSource))
			.DisposeWith(_disposables);

		CutStepCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Subscribe(ex => _messagePanel.AddError($"Cut failed: {ex.Message}", ClipboardSource))
			.DisposeWith(_disposables);

		PasteStepCommand.ThrownExceptions
			.ObserveOn(RxSchedulers.MainThreadScheduler)
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
		var csvText = SerializeStepsForClipboard(steps);
		await _clipboard.SetTextAsync(csvText);
	}

	private async Task CutStepsAsync()
	{
		if (_clipboard is null || _recipeGrid.SelectedRowIndices.Count == 0)
		{
			return;
		}

		var steps = _recipeGrid.CollectSelectedSteps();
		var csvText = SerializeStepsForClipboard(steps);
		await _clipboard.SetTextAsync(csvText);

		var result = _coordinator.RemoveSteps(_recipeGrid.SelectedRowIndices);
		if (result.IsFailed)
		{
			_messagePanel.AddError(result.Errors[0].Message, ClipboardSource);
			return;
		}

		_recipeGrid.RequestSelection(result.Value);
	}

	private async Task PasteStepsAsync()
	{
		if (_clipboard is null)
		{
			return;
		}

		var csvText = await _clipboard.TryGetTextAsync();
		if (string.IsNullOrWhiteSpace(csvText))
		{
			return;
		}

		var recipeResult = DeserializeStepsFromClipboard(csvText);
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
			return;
		}

		_recipeGrid.RequestSelection(insertResult.Value);
	}

	private string SerializeStepsForClipboard(IReadOnlyList<Step> steps)
	{
		var recipe = new Recipe(steps.ToImmutableList());
		return _clipboardSerializer.SerializeSteps(recipe);
	}

	private Result<Recipe> DeserializeStepsFromClipboard(string csv)
	{
		var result = _clipboardSerializer.DeserializeSteps(csv);
		if (result.IsFailed)
		{
			return result;
		}

		var validationResult = _importedRecipeValidator.Validate(result.Value);
		if (validationResult.IsFailed)
		{
			return validationResult.ToResult<Recipe>();
		}

		return result;
	}
}
