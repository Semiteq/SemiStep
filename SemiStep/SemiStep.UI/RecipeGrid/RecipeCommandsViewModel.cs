using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using ReactiveUI;

using SemiStep.UI.Coordinator;

namespace SemiStep.UI.RecipeGrid;

public class RecipeCommandsViewModel : ReactiveObject, IDisposable
{
	private readonly RecipeCoordinator _coordinator;
	private readonly CompositeDisposable _disposables = new();
	private readonly BehaviorSubject<bool> _canUndo = new(false);
	private readonly BehaviorSubject<bool> _canRedo = new(false);
	private readonly RecipeGridViewModel _recipeGrid;

	public RecipeCommandsViewModel(
		RecipeCoordinator coordinator,
		RecipeGridViewModel recipeGrid)
	{
		_coordinator = coordinator;
		_recipeGrid = recipeGrid;

		_coordinator.Mutated += OnCoordinatorMutated;
		_disposables.Add(Disposable.Create(() => _coordinator.Mutated -= OnCoordinatorMutated));
		_disposables.Add(_canUndo);
		_disposables.Add(_canRedo);

		var canDelete = _recipeGrid
			.WhenAnyValue(x => x.CanDeleteStep);

		AddStepCommand = ReactiveCommand.Create(AddStep);
		DeleteStepCommand = ReactiveCommand.Create(DeleteStep, canDelete);
		UndoCommand = ReactiveCommand.Create(Undo, _canUndo);
		RedoCommand = ReactiveCommand.Create(Redo, _canRedo);

		AddStepCommand.DisposeWith(_disposables);
		DeleteStepCommand.DisposeWith(_disposables);
		UndoCommand.DisposeWith(_disposables);
		RedoCommand.DisposeWith(_disposables);
	}

	private void OnCoordinatorMutated()
	{
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

		var result = _recipeGrid.SelectedRowIndex >= 0
			? _coordinator.InsertStep(_recipeGrid.SelectedRowIndex + 1, firstActionId)
			: _coordinator.AppendStep(firstActionId);

		if (result.IsSuccess)
		{
			_recipeGrid.RequestSelection(result.Value);
		}
	}

	private void DeleteStep()
	{
		var indices = _recipeGrid.SelectedRowIndices;
		if (indices.Count == 0)
		{
			return;
		}

		var result = indices.Count == 1
			? _coordinator.RemoveStep(indices[0])
			: _coordinator.RemoveSteps(indices);

		if (result.IsSuccess)
		{
			_recipeGrid.RequestSelection(result.Value);
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
