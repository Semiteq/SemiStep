using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using ReactiveUI;

using SemiStep.UI.Coordinator;

namespace SemiStep.UI.RecipeGrid;

public class RecipeCommandsViewModel : ReactiveObject, IDisposable
{
	private readonly RecipeMutationCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly RecipeGridViewModel _recipeGrid;

	public RecipeCommandsViewModel(
		RecipeMutationCoordinator coordinator,
		RecipeGridViewModel recipeGrid)
	{
		_coordinator = coordinator;
		_recipeGrid = recipeGrid;

		var canUndo = _coordinator.StateChanged
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Select(_ => _coordinator.CanUndo)
			.StartWith(false);

		var canRedo = _coordinator.StateChanged
			.ObserveOn(RxSchedulers.MainThreadScheduler)
			.Select(_ => _coordinator.CanRedo)
			.StartWith(false);

		var canDelete = _recipeGrid
			.WhenAnyValue(x => x.CanDeleteStep);

		AddStepCommand = ReactiveCommand.Create(AddStep);
		DeleteStepCommand = ReactiveCommand.Create(DeleteStep, canDelete);
		UndoCommand = ReactiveCommand.Create(Undo, canUndo);
		RedoCommand = ReactiveCommand.Create(Redo, canRedo);

		AddStepCommand.DisposeWith(_disposables);
		DeleteStepCommand.DisposeWith(_disposables);
		UndoCommand.DisposeWith(_disposables);
		RedoCommand.DisposeWith(_disposables);
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
		var firstActionId = _coordinator.QueryService.GetDefaultActionId();

		if (_recipeGrid.SelectedRowIndex >= 0)
		{
			var newRowIndex = _recipeGrid.SelectedRowIndex + 1;
			_coordinator.InsertStep(newRowIndex, firstActionId);
		}
		else
		{
			_coordinator.AppendStep(firstActionId);
		}
	}

	private void DeleteStep()
	{
		var indices = _recipeGrid.SelectedRowIndices;
		if (indices.Count == 0)
		{
			return;
		}

		if (indices.Count == 1)
		{
			_coordinator.RemoveStep(indices[0]);
		}
		else
		{
			_coordinator.RemoveSteps(indices);
		}
	}

	private void Undo()
	{
		_coordinator.Undo();
	}

	private void Redo()
	{
		_coordinator.Redo();
	}
}
