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
	RecipeHistoryManager historyManager)
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

	/// <summary>
	/// Reverts to the previous recipe state.
	/// </summary>
	/// <returns>The restored snapshot, or null if nothing to undo.</returns>
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

	/// <summary>
	/// Reapplies a previously undone state.
	/// </summary>
	/// <returns>The restored snapshot, or null if nothing to redo.</returns>
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
		// Note: LoadRecipe() preserves history per design decision
		throw new NotImplementedException();
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
