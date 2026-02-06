using Core.Analysis;
using Core.Entities;

using Domain.Services;
using Domain.State;

using Shared;
using Shared.Registries;

namespace Domain.Facade;

public sealed class DomainFacade(
	IActionRegistry actionRegistry,
	IPropertyRegistry propertyRegistry,
	IColumnRegistry columnRegistry,
	IGroupRegistry groupRegistry,
	CoreService coreService,
	RecipeStateManager stateManager,
	RecipeHistoryManager historyManager,
	PlcSyncService plcSyncService)
	: IDisposable
{
	private bool _disposed;

	public Recipe CurrentRecipe => stateManager.Current;

	public bool IsDirty => stateManager.IsDirty;
	public bool IsValid => stateManager.IsValid;
	public RecipeSnapshot Snapshot => stateManager.LastSnapshot ?? RecipeSnapshot.Empty;


	public bool CanUndo => historyManager.CanUndo;
	public bool CanRedo => historyManager.CanRedo;

	public void Initialize(AppConfiguration appConfig)
	{
		actionRegistry.Initialize(appConfig.Actions);
		propertyRegistry.Initialize(appConfig.Properties);
		columnRegistry.Initialize(appConfig.Columns);
		groupRegistry.Initialize(appConfig.Groups);

		coreService.NewRecipe();
	}

	public void NewRecipe()
	{
		historyManager.Clear();
		coreService.NewRecipe();
	}

	public void InsertStep(int index, int actionId)
	{
		historyManager.Push(stateManager.Current);
		var snapshot = coreService.InsertStep(index, actionId);
		stateManager.Update(snapshot);
	}

	public void AppendStep(int actionId)
	{
		historyManager.Push(stateManager.Current);
		var snapshot = coreService.AppendStep(actionId);
		stateManager.Update(snapshot);
	}

	public void ChangeStepAction(int stepIndex, int newActionId)
	{
		historyManager.Push(stateManager.Current);
		var snapshot = coreService.ChangeStepAction(stepIndex, newActionId);
		stateManager.Update(snapshot);
	}

	public void RemoveStep(int index)
	{
		historyManager.Push(stateManager.Current);
		var snapshot = coreService.RemoveStep(index);
		stateManager.Update(snapshot);
	}

	public void UpdateStepProperty(int stepIndex, string columnKey, object value)
	{
		historyManager.Push(stateManager.Current);
		var snapshot = coreService.UpdateStepProperty(stepIndex, columnKey, value);
		stateManager.Update(snapshot);
	}

	public RecipeSnapshot? Undo()
	{
		var previous = historyManager.Undo(stateManager.Current);
		if (previous is null)
		{
			return null;
		}

		var snapshot = coreService.AnalyzeRecipe(previous);
		stateManager.Update(snapshot);
		return snapshot;
	}

	public RecipeSnapshot? Redo()
	{
		var next = historyManager.Redo(stateManager.Current);
		if (next is null)
		{
			return null;
		}

		var snapshot = coreService.AnalyzeRecipe(next);
		stateManager.Update(snapshot);
		return snapshot;
	}

	public void LoadRecipe()
	{
		throw new NotImplementedException();
	}

	public async Task LoadFromPlcAsync(CancellationToken ct = default)
	{
		var recipe = await plcSyncService.LoadRecipeAsync(ct);
		historyManager.Clear();
		var snapshot = coreService.AnalyzeRecipe(recipe);
		stateManager.Update(snapshot);
		stateManager.MarkSaved();
	}

	public void SaveRecipe()
	{
		throw new NotImplementedException();
	}

	public void MarkSaved()
	{
		stateManager.MarkSaved();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}
}
