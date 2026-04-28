using System.Collections.Immutable;

using ClipBoard;

using Domain;
using Domain.Facade;
using Domain.Helpers;

using FluentResults;

using TypesShared.Config;
using TypesShared.Core;
using TypesShared.Plc;

namespace UI.Coordinator;

public sealed class RecipeQueryService(
	RecipeWorkspace workspace,
	PlcLifecycleManager plcLifecycleManager,
	ClipboardService clipboardService,
	ImportedRecipeValidator importedRecipeValidator,
	ConfigRegistry configRegistry)
{
	public Recipe CurrentRecipe => workspace.CurrentRecipe;

	public RecipeSnapshot Snapshot => workspace.Snapshot.IsSuccess
		? workspace.Snapshot.Value
		: RecipeSnapshot.Empty;

	public bool IsDirty => workspace.IsDirty;
	public bool CanUndo => workspace.CanUndo;
	public bool CanRedo => workspace.CanRedo;
	public bool IsConnected => plcLifecycleManager.IsConnected;

	public IObservable<PlcExecutionInfo> ExecutionState => plcLifecycleManager.ExecutionState;
	public bool IsRecipeActive => plcLifecycleManager.IsRecipeActive;
	public PlcSyncStatus SyncStatus => plcLifecycleManager.SyncStatus;
	public DateTimeOffset? LastSyncTime => plcLifecycleManager.LastSyncTime;
	public bool IsSyncEnabled => plcLifecycleManager.IsSyncEnabled;

	public CellState GetCellState(GridColumnDefinition column, ActionDefinition action)
	{
		return CellStateResolver.GetCellState(column, action);
	}

	public int GetDefaultActionId()
	{
		return configRegistry.GetAllActions().FirstOrDefault()?.Id
			?? throw new InvalidOperationException("No actions are defined in the configuration.");
	}

	public string SerializeStepsForClipboard(IReadOnlyList<Step> steps)
	{
		var recipe = new Recipe(steps.ToImmutableList());
		return clipboardService.SerializeSteps(recipe);
	}

	public Result<Recipe> DeserializeStepsFromClipboard(string csv)
	{
		var result = clipboardService.DeserializeSteps(csv);
		if (result.IsFailed)
		{
			return result;
		}

		var validationResult = importedRecipeValidator.Validate(result.Value);
		if (validationResult.IsFailed)
		{
			return validationResult.ToResult<Recipe>();
		}

		return result;
	}
}
