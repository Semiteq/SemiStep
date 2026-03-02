using Core.Analysis;
using Core.Entities;
using Core.Formulas;

using Shared.Entities;
using Shared.Registries;

namespace Core.Services;

public sealed class CoreFacade(
	RecipeMutator mutator,
	RecipeAnalyzer analyzer,
	FormulaApplicationCoordinator formulaCoordinator)
{
	public RecipeSnapshot Analyze(Recipe recipe)
	{
		return analyzer.Analyze(recipe);
	}

	public RecipeSnapshot AppendStep(
		Recipe recipe,
		ActionDefinition action,
		IPropertyRegistry propertyRegistry,
		IGroupRegistry groupRegistry)
	{
		var newRecipe = mutator.AddStep(recipe, action, propertyRegistry, groupRegistry);

		return analyzer.Analyze(newRecipe);
	}

	public RecipeSnapshot InsertStep(
		Recipe recipe,
		int stepIndex,
		ActionDefinition action,
		IPropertyRegistry propertyRegistry,
		IGroupRegistry groupRegistry)
	{
		ValidateIndexOrThrow(recipe, stepIndex);

		var newRecipe = mutator.InsertStep(recipe, stepIndex, action, propertyRegistry, groupRegistry);

		return analyzer.Analyze(newRecipe);
	}

	public RecipeSnapshot RemoveStep(Recipe recipe, int stepIndex)
	{
		ValidateIndexOrThrow(recipe, stepIndex);

		var newRecipe = mutator.RemoveStep(recipe, stepIndex);

		return analyzer.Analyze(newRecipe);
	}

	public RecipeSnapshot ChangeStepAction(
		Recipe recipe,
		int stepIndex,
		ActionDefinition newAction,
		IPropertyRegistry propertyRegistry,
		IGroupRegistry groupRegistry)
	{
		ValidateIndexOrThrow(recipe, stepIndex);

		var newRecipe = mutator.ChangeStepAction(recipe, stepIndex, newAction, propertyRegistry, groupRegistry);

		return analyzer.Analyze(newRecipe);
	}

	public RecipeSnapshot UpdateProperty(
		Recipe recipe,
		int stepIndex,
		ColumnId columnId,
		PropertyValue value,
		PropertyDefinition propertyDefinition,
		ActionDefinition actionDefinition,
		FormulaDefinition? formulaDefinition = null)
	{
		ValidateIndexOrThrow(recipe, stepIndex);

		PropertyValidator.ThrowIfInvalid(propertyDefinition, value.Value);
		var mutatedRecipe = mutator.UpdateProperty(recipe, stepIndex, columnId, value);

		var recalculatedStep = formulaCoordinator.ApplyIfExists(
			mutatedRecipe.Steps[stepIndex],
			actionDefinition,
			columnId,
			formulaDefinition);

		var recalculatedRecipe = mutatedRecipe with
		{
			Steps = mutatedRecipe.Steps.SetItem(stepIndex, recalculatedStep)
		};

		return analyzer.Analyze(recalculatedRecipe);
	}

	private static void ValidateIndexOrThrow(Recipe recipe, int index)
	{
		if (index < 0 || index > recipe.Steps.Count)
		{
			throw new IndexOutOfRangeException(
				$"Index {index} is out of range for recipe with {recipe.Steps.Count} steps.");
		}
	}
}
