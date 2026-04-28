using FluentResults;

using TypesShared.Core;

namespace TypesShared.Domain;

public interface ICoreService
{
	Result<RecipeSnapshot> AnalyzeRecipe(Recipe recipe);

	Result<RecipeSnapshot> AppendStep(
		Recipe recipe,
		int action);

	Result<RecipeSnapshot> InsertStep(
		Recipe recipe,
		int stepIndex,
		int actionId);

	Result<RecipeSnapshot> RemoveStep(
		Recipe recipe,
		int stepIndex);

	Result<RecipeSnapshot> InsertSteps(
		Recipe recipe,
		int startIndex,
		IReadOnlyList<Step> steps);

	Result<RecipeSnapshot> RemoveSteps(
		Recipe recipe,
		IReadOnlyList<int> indices);

	Result<RecipeSnapshot> ChangeStepAction(
		Recipe recipe,
		int stepIndex,
		int newActionId);

	Result<RecipeSnapshot> UpdateStepProperty(
		Recipe recipe,
		int stepIndex,
		string columnKey,
		PropertyValue value);
}
