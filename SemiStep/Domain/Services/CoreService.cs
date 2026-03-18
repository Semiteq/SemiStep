using Domain.State;

using Shared.Config.Contracts;
using Shared.Core;
using Shared.ServiceContracts;

namespace Domain.Services;

internal sealed class CoreService(
	ICoreService coreFacade,
	RecipeStateManager stateManager,
	IActionRegistry actionRegistry,
	IPropertyRegistry propertyRegistry,
	IGroupRegistry groupRegistry)
{
	public RecipeSnapshot NewRecipe()
	{
		var recipeSnapshot = coreFacade.Analyze(Recipe.Empty);
		stateManager.Reset();
		stateManager.Update(recipeSnapshot);

		return recipeSnapshot;
	}

	public RecipeSnapshot AnalyzeRecipe(Recipe recipe)
	{
		return coreFacade.Analyze(recipe);
	}

	public RecipeSnapshot AppendStep(int actionId)
	{
		var action = actionRegistry.GetAction(actionId);
		var recipeSnapshot = coreFacade.AppendStep(stateManager.Current, action, propertyRegistry, groupRegistry);

		return recipeSnapshot;
	}

	public RecipeSnapshot InsertStep(int index, int actionId)
	{
		var action = actionRegistry.GetAction(actionId);
		var recipeSnapshot =
			coreFacade.InsertStep(stateManager.Current, index, action, propertyRegistry, groupRegistry);

		return recipeSnapshot;
	}

	public RecipeSnapshot ChangeStepAction(int stepIndex, int newActionId)
	{
		var newAction = actionRegistry.GetAction(newActionId);
		var recipeSnapshot = coreFacade.ChangeStepAction(stateManager.Current, stepIndex, newAction, propertyRegistry,
			groupRegistry);

		return recipeSnapshot;
	}

	public RecipeSnapshot RemoveStep(int index)
	{
		var recipeSnapshot = coreFacade.RemoveStep(stateManager.Current, index);

		return recipeSnapshot;
	}

	public RecipeSnapshot InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		var recipeSnapshot = coreFacade.InsertSteps(stateManager.Current, startIndex, steps);

		return recipeSnapshot;
	}

	public RecipeSnapshot RemoveSteps(IReadOnlyList<int> indices)
	{
		var recipeSnapshot = coreFacade.RemoveSteps(stateManager.Current, indices);

		return recipeSnapshot;
	}

	public RecipeSnapshot UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		var step = stateManager.Current.Steps[stepIndex];
		var action = actionRegistry.GetAction(step.ActionKey);

		var actionColumn = action.Columns.FirstOrDefault(c => c.Key == columnKey)
			?? throw new KeyNotFoundException($"Column '{columnKey}' is not defined for action '{action.Id}'");
		var property = propertyRegistry.GetProperty(actionColumn.PropertyTypeId);

		var columnId = new ColumnId(columnKey);

		var result = coreFacade.UpdateProperty(
			stateManager.Current,
			stepIndex,
			columnId,
			value,
			property,
			action);

		return result;
	}
}
