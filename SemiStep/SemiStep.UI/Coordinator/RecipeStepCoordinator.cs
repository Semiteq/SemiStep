using System.Linq;

using FluentResults;

using SemiStep.Core.Recipes;


namespace SemiStep.UI.Coordinator;

internal sealed class RecipeStepCoordinator(
	RecipeWorkspace workspace,
	RecipeEditor editor,
	Func<Recipe> getCurrentRecipe,
	Action<Result> setLastRecipeResult,
	Action<int?> setSuggestedSelection,
	Action<MutationSignal> publishSignal,
	Action rebuildMessagePanel)
{
	public Result AppendStep(int actionId)
	{
		var result = editor.AppendStep(actionId);
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		setSuggestedSelection(getCurrentRecipe().StepCount - 1);
		publishSignal(new MutationSignal.StepAppended(getCurrentRecipe().StepCount - 1));
		return result;
	}

	public Result InsertStep(int index, int actionId)
	{
		var result = editor.InsertStep(index, actionId);
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		setSuggestedSelection(index);
		publishSignal(new MutationSignal.StepsInserted(index, 1));
		return result;
	}

	public Result RemoveStep(int index)
	{
		var result = editor.RemoveStep(index);
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		var currentRecipe = getCurrentRecipe();
		setSuggestedSelection(currentRecipe.StepCount > 0
			? Math.Min(index, currentRecipe.StepCount - 1)
			: null);
		publishSignal(new MutationSignal.StepRemoved(index));
		return result;
	}

	public Result RemoveSteps(IReadOnlyList<int> indices)
	{
		var result = editor.RemoveSteps(indices);
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		var currentRecipe = getCurrentRecipe();
		setSuggestedSelection(currentRecipe.StepCount > 0
			? Math.Min(indices.Min(), currentRecipe.StepCount - 1)
			: null);
		publishSignal(new MutationSignal.StepsRemoved([.. indices]));
		return result;
	}

	public Result InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		var result = editor.InsertSteps(startIndex, steps);
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		setSuggestedSelection(startIndex);
		publishSignal(new MutationSignal.StepsInserted(startIndex, steps.Count));
		return result;
	}

	public Result ChangeStepAction(int stepIndex, int newActionId)
	{
		var result = editor.ChangeStepAction(stepIndex, newActionId);
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		setSuggestedSelection(stepIndex);
		publishSignal(new MutationSignal.StepActionChanged(stepIndex));
		return result;
	}

	public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		var result = editor.UpdateStepProperty(stepIndex, columnKey, value);
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		publishSignal(new MutationSignal.PropertyUpdated(stepIndex));
		return result;
	}

	public Result Undo()
	{
		var result = workspace.Undo();
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		setSuggestedSelection(null);
		publishSignal(new MutationSignal.RecipeReplaced());
		return result;
	}

	public Result Redo()
	{
		var result = workspace.Redo();
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		setSuggestedSelection(null);
		publishSignal(new MutationSignal.RecipeReplaced());
		return result;
	}

	public Result NewRecipe()
	{
		var result = workspace.Reset();
		setLastRecipeResult(result);
		rebuildMessagePanel();

		if (result.IsFailed)
		{
			return result;
		}

		setSuggestedSelection(null);
		publishSignal(new MutationSignal.RecipeReplaced());
		return result;
	}
}
