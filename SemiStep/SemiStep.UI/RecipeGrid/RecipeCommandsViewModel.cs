using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Microsoft.Extensions.Logging;

using ReactiveUI;

using SemiStep.UI.Coordinator;
using SemiStep.UI.Localization;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeGrid;

public class RecipeCommandsViewModel : ReactiveObject, IDisposable
{
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly BehaviorSubject<bool> _canUndo = new(false);
	private readonly BehaviorSubject<bool> _canRedo = new(false);
	private readonly IRecipeGridSurface _recipeGrid;
	private readonly MessagePanelViewModel _messagePanel;
	private readonly ILogger<RecipeCommandsViewModel> _logger;

	public RecipeCommandsViewModel(
		RecipeCoordinator coordinator,
		IRecipeGridSurface recipeGrid,
		MessagePanelViewModel messagePanel,
		ILogger<RecipeCommandsViewModel> logger)
	{
		_coordinator = coordinator;
		_recipeGrid = recipeGrid;
		_messagePanel = messagePanel;
		_logger = logger;

		_coordinator.Mutated += OnCoordinatorMutated;
		_disposables.Add(Disposable.Create(() => _coordinator.Mutated -= OnCoordinatorMutated));
		_disposables.Add(_canUndo);
		_disposables.Add(_canRedo);

		var canDelete = _recipeGrid.CanDeleteStep;

		var canEdit = _coordinator.CanEditRecipe;

		AddStepCommand = ReactiveCommand.Create(AddStep, canEdit);
		DeleteStepCommand = ReactiveCommand.Create(DeleteStep, canEdit.CombineLatest(canDelete, (left, right) => left && right));
		UndoCommand = ReactiveCommand.Create(Undo, canEdit.CombineLatest(_canUndo, (left, right) => left && right));
		RedoCommand = ReactiveCommand.Create(Redo, canEdit.CombineLatest(_canRedo, (left, right) => left && right));

		AddStepCommand.DisposeWith(_disposables);
		DeleteStepCommand.DisposeWith(_disposables);
		UndoCommand.DisposeWith(_disposables);
		RedoCommand.DisposeWith(_disposables);

		AddStepCommand.ReportThrownExceptions(_messagePanel, _logger, new LocalizedText(nameof(Resources.AddStepFailed)))
			.DisposeWith(_disposables);

		DeleteStepCommand.ReportThrownExceptions(_messagePanel, _logger, new LocalizedText(nameof(Resources.DeleteStepFailed)))
			.DisposeWith(_disposables);

		UndoCommand.ReportThrownExceptions(_messagePanel, _logger, new LocalizedText(nameof(Resources.UndoFailed)))
			.DisposeWith(_disposables);

		RedoCommand.ReportThrownExceptions(_messagePanel, _logger, new LocalizedText(nameof(Resources.RedoFailed)))
			.DisposeWith(_disposables);
	}

	private void OnCoordinatorMutated(MutationSignal signal)
	{
		_ = signal;
		_canUndo.OnNext(_coordinator.CanUndo);
		_canRedo.OnNext(_coordinator.CanRedo);
	}

	public ReactiveCommand<Unit, Unit> AddStepCommand { get; }

	public ReactiveCommand<Unit, Unit> DeleteStepCommand { get; }

	public ReactiveCommand<Unit, Unit> UndoCommand { get; }

	public ReactiveCommand<Unit, Unit> RedoCommand { get; }

	public void Dispose()
	{
		_disposables.Dispose();
		GC.SuppressFinalize(this);
	}

	private void AddStep()
	{
		var firstActionId = _coordinator.GetDefaultActionId();

		var result = _recipeGrid.SelectedStepIndex >= 0
			? _coordinator.InsertStep(_recipeGrid.SelectedStepIndex + 1, firstActionId)
			: _coordinator.AppendStep(firstActionId);

		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result);
			return;
		}

		_recipeGrid.RequestSelection(result.Value);
	}

	private void DeleteStep()
	{
		var indices = _recipeGrid.SelectedStepIndices;
		if (indices.Count == 0)
		{
			return;
		}

		var result = indices.Count == 1
			? _coordinator.RemoveStep(indices[0])
			: _coordinator.RemoveSteps(indices);

		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result);
			return;
		}

		_recipeGrid.RequestSelection(result.Value);
	}

	private void Undo()
	{
		var result = _coordinator.Undo();
		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result);
		}
	}

	private void Redo()
	{
		var result = _coordinator.Redo();
		if (result.IsFailed)
		{
			_messagePanel.ReportFailure(result);
		}
	}
}
