using Domain.State;

using FluentResults;

using TypesShared.Config;
using TypesShared.Core;
using TypesShared.Domain;

namespace Domain.Facade;

/// <summary>
/// Handles recipe mutation operations: append, insert, remove, change action, update property.
/// </summary>
internal sealed class RecipeEditService(
	ICoreService coreService,
	RecipeStateManager stateManager,
	RecipeHistoryManager historyManager,
	IPropertyParser propertyParser,
	ConfigRegistry configRegistry,
	IPlcSyncService syncService,
	Func<bool> isSyncEnabled)
{
	public Result AppendStep(int actionId)
	{
		var snapshot = coreService.AppendStep(stateManager.Current, actionId);

		return ApplyIfSucceeded(snapshot);
	}

	public Result InsertStep(int index, int actionId)
	{
		var snapshot = coreService.InsertStep(stateManager.Current, index, actionId);

		return ApplyIfSucceeded(snapshot);
	}

	public Result RemoveStep(int index)
	{
		var snapshot = coreService.RemoveStep(stateManager.Current, index);

		return ApplyIfSucceeded(snapshot);
	}

	public Result InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		var snapshot = coreService.InsertSteps(stateManager.Current, startIndex, steps);

		return ApplyIfSucceeded(snapshot);
	}

	public Result RemoveSteps(IReadOnlyList<int> indices)
	{
		var snapshot = coreService.RemoveSteps(stateManager.Current, indices);

		return ApplyIfSucceeded(snapshot);
	}

	public Result ChangeStepAction(int stepIndex, int newActionId)
	{
		var snapshot = coreService.ChangeStepAction(stateManager.Current, stepIndex, newActionId);

		return ApplyIfSucceeded(snapshot);
	}

	public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		var propertyResult = ResolvePropertyDefinition(stepIndex, columnKey);
		if (propertyResult.IsFailed)
		{
			return propertyResult.ToResult();
		}

		var parseResult = propertyParser.Parse(value, propertyResult.Value);
		if (parseResult.IsFailed)
		{
			return parseResult.ToResult();
		}

		var snapshot = coreService.UpdateStepProperty(
			stateManager.Current, stepIndex, columnKey, parseResult.Value);

		return ApplyIfSucceeded(snapshot);
	}

	private Result ApplyIfSucceeded(Result<RecipeSnapshot> snapshot)
	{
		if (snapshot.IsFailed)
		{
			return snapshot.ToResult();
		}

		historyManager.Push(stateManager.Current);
		stateManager.Update(snapshot);

		if (isSyncEnabled())
		{
			syncService.NotifyRecipeChanged(stateManager.Current, stateManager.IsValid);
		}

		return Result.Ok().WithReasons(snapshot.Reasons);
	}

	private Result<PropertyTypeDefinition> ResolvePropertyDefinition(int stepIndex, string columnKey)
	{
		var validationResult = ValidateStepIndex(stepIndex);
		if (validationResult.IsFailed)
		{
			return validationResult.ToResult<PropertyTypeDefinition>();
		}

		return configRegistry.ResolvePropertyType(stateManager.Current, stepIndex, columnKey);
	}

	private Result ValidateStepIndex(int stepIndex)
	{
		var recipe = stateManager.Current;
		if (stepIndex < 0 || stepIndex >= recipe.Steps.Count)
		{
			return Result.Fail($"Step index {stepIndex} is out of range for recipe with {recipe.Steps.Count} steps");
		}
		return Result.Ok();
	}
}
