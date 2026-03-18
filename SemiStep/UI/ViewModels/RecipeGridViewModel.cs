using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using Domain.Facade;

using ReactiveUI;

using Shared.Config.Contracts;
using Shared.Core;

using UI.Services;

namespace UI.ViewModels;

public partial class RecipeGridViewModel : ReactiveObject, IDisposable
{
	private readonly ObservableAsPropertyHelper<bool> _canDeleteStep;
	private readonly CompositeDisposable _disposables = new();
	private readonly DomainFacade _domainFacade;
	private readonly LogPanelViewModel _logPanel;
	private readonly Action _notifyStateChanged;
	private readonly INotificationService _notificationService;
	private readonly Subject<Unit> _stateRefresh = new();
	private int _selectedRowIndex = -1;
	private IReadOnlyList<int> _selectedRowIndices = [];

	public RecipeGridViewModel(
		DomainFacade domainFacade,
		IActionRegistry actionRegistry,
		IGroupRegistry groupRegistry,
		IColumnRegistry columnRegistry,
		IPropertyRegistry propertyRegistry,
		LogPanelViewModel logPanel,
		INotificationService notificationService,
		Action notifyStateChanged)
	{
		_domainFacade = domainFacade;
		ActionRegistry = actionRegistry;
		GroupRegistry = groupRegistry;
		ColumnRegistry = columnRegistry;
		PropertyRegistry = propertyRegistry;
		_logPanel = logPanel;
		_notificationService = notificationService;
		_notifyStateChanged = notifyStateChanged;

		RecipeRows = new ObservableCollection<RecipeRowViewModel>();

		_canDeleteStep = this
			.WhenAnyValue(x => x.SelectedRowIndices)
			.Select(indices => indices.Count > 0)
			.ToProperty(this, x => x.CanDeleteStep)
			.DisposeWith(_disposables);

		var canUndo = _stateRefresh
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => _domainFacade.CanUndo)
			.StartWith(false);

		var canRedo = _stateRefresh
			.ObserveOn(RxApp.MainThreadScheduler)
			.Select(_ => _domainFacade.CanRedo)
			.StartWith(false);

		AddStepCommand = ReactiveCommand.Create(AddStep);
		DeleteStepCommand = ReactiveCommand.Create(DeleteStep, this.WhenAnyValue(x => x.CanDeleteStep));
		UndoCommand = ReactiveCommand.Create(Undo, canUndo);
		RedoCommand = ReactiveCommand.Create(Redo, canRedo);

		_stateRefresh.DisposeWith(_disposables);
	}

	public IActionRegistry ActionRegistry { get; }

	public IGroupRegistry GroupRegistry { get; }

	public IColumnRegistry ColumnRegistry { get; }

	public IPropertyRegistry PropertyRegistry { get; }

	public ObservableCollection<RecipeRowViewModel> RecipeRows { get; }

	public ReactiveCommand<Unit, Unit> AddStepCommand { get; }

	public ReactiveCommand<Unit, Unit> DeleteStepCommand { get; }

	public ReactiveCommand<Unit, Unit> UndoCommand { get; }

	public ReactiveCommand<Unit, Unit> RedoCommand { get; }

	public bool CanDeleteStep => _canDeleteStep.Value;

	public int SelectedRowIndex
	{
		get => _selectedRowIndex;
		set => this.RaiseAndSetIfChanged(ref _selectedRowIndex, value);
	}

	public IReadOnlyList<int> SelectedRowIndices
	{
		get => _selectedRowIndices;
		set => this.RaiseAndSetIfChanged(ref _selectedRowIndices, value);
	}

	public void Dispose()
	{
		_disposables.Dispose();

		foreach (var row in RecipeRows)
		{
			row.Dispose();
		}

		GC.SuppressFinalize(this);
	}

	public void Initialize()
	{
		RebuildAllRows(_domainFacade.CurrentRecipe);
		RefreshAfterMutation();
	}

	public void DeleteStep()
	{
		var indices = _selectedRowIndices;
		if (indices.Count == 0)
		{
			return;
		}

		var sortedDesc = indices.OrderByDescending(i => i).ToList();

		if (indices.Count == 1)
		{
			var indexToDelete = indices[0];
			_domainFacade.RemoveStep(indexToDelete);
			RemoveRow(indexToDelete);
		}
		else
		{
			_domainFacade.RemoveSteps(indices);
			foreach (var i in sortedDesc)
			{
				RecipeRows[i].Dispose();
				RecipeRows.RemoveAt(i);
			}

			RenumberRowsFrom(0);
		}

		RefreshAfterMutation();

		var firstDeleted = sortedDesc[^1];
		if (RecipeRows.Count > 0)
		{
			SelectedRowIndex = Math.Min(firstDeleted, RecipeRows.Count - 1);
		}
		else
		{
			SelectedRowIndex = -1;
		}
	}

	private void AddStep()
	{
		var firstAction = ActionRegistry.GetAll().First();
		int newRowIndex;

		if (SelectedRowIndex >= 0)
		{
			newRowIndex = SelectedRowIndex + 1;
			_domainFacade.InsertStep(newRowIndex, firstAction.Id);
			var insertedStep = _domainFacade.CurrentRecipe.Steps[newRowIndex];
			InsertRow(newRowIndex, insertedStep);
		}
		else
		{
			newRowIndex = RecipeRows.Count;
			_domainFacade.AppendStep(firstAction.Id);
			var appendedStep = _domainFacade.CurrentRecipe.Steps[newRowIndex];
			AppendRow(appendedStep);
		}

		RefreshAfterMutation();
		SelectedRowIndex = newRowIndex;
	}

	private void Undo()
	{
		var snapshot = _domainFacade.Undo();
		if (snapshot is not null)
		{
			RebuildAllRows(snapshot.Recipe);
			RefreshAfterMutation();
		}
	}

	private void Redo()
	{
		var snapshot = _domainFacade.Redo();
		if (snapshot is not null)
		{
			RebuildAllRows(snapshot.Recipe);
			RefreshAfterMutation();
		}
	}

	private void OnCellValueChanged(RecipeRowViewModel row, string columnKey, string? value)
	{
		if (value is null)
		{
			return;
		}

		var stepIndex = RecipeRows.IndexOf(row);
		if (stepIndex < 0)
		{
			return;
		}

		try
		{
			_domainFacade.UpdateStepProperty(stepIndex, columnKey, value);

			var updatedStep = _domainFacade.CurrentRecipe.Steps[stepIndex];
			RecipeRows[stepIndex].UpdateStep(updatedStep);
		}
		catch (Exception ex)
		{
			_notificationService.ShowError($"Step {stepIndex + 1}: {ex.Message}");
		}

		RefreshAfterMutation();
	}

	public void OnActionChanged(RecipeRowViewModel row, int newActionId)
	{
		var stepIndex = RecipeRows.IndexOf(row);
		if (stepIndex < 0)
		{
			return;
		}

		try
		{
			_domainFacade.ChangeStepAction(stepIndex, newActionId);
			var updatedStep = _domainFacade.CurrentRecipe.Steps[stepIndex];
			var newAction = ActionRegistry.GetAction(newActionId);
			RecipeRows[stepIndex].Dispose();
			RecipeRows[stepIndex] = CreateRowViewModel(updatedStep, newAction, stepIndex + 1);
		}
		catch (Exception ex)
		{
			_notificationService.ShowError(
				$"Step {stepIndex + 1}: Failed to change action - {ex.Message}");
		}

		RefreshAfterMutation();
	}

	public void RefreshAfterMutation()
	{
		_logPanel.RefreshReasons(_domainFacade.Snapshot.Errors, _domainFacade.Snapshot.Warnings);
		RefreshStepStartTimes();
		NotifyStateChanged();
	}

	private void NotifyStateChanged()
	{
		_stateRefresh.OnNext(Unit.Default);
		_notifyStateChanged();
	}
}
