using Core.Analysis;
using Core.Entities;
using Core.Formulas;

using Shared.Entities;

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
		IReadOnlyList<PropertyDefinition> properties)
	{
		var newRecipe = mutator.AddStep(recipe, action, properties);
		return analyzer.Analyze(newRecipe);
	}

	public RecipeSnapshot InsertStep(
		Recipe recipe,
		int stepIndex,
		ActionDefinition action,
		IReadOnlyList<PropertyDefinition> properties)
	{
		if (stepIndex < 0 || stepIndex >= recipe.Steps.Count)
		{
			throw new IndexOutOfRangeException(
				$"Index {stepIndex} is out of range for recipe with {recipe.Steps.Count} steps.");
		}

		var newRecipe = mutator.InsertStep(recipe, stepIndex, action, properties);
		return analyzer.Analyze(newRecipe);
	}

	public RecipeSnapshot RemoveStep(Recipe recipe, int stepIndex)
	{
		if (stepIndex < 0 || stepIndex >= recipe.Steps.Count)
		{
			throw new IndexOutOfRangeException(
				$"Index {stepIndex} is out of range for recipe with {recipe.Steps.Count} steps.");
		}

		var newRecipe = mutator.RemoveStep(recipe, stepIndex);
		return analyzer.Analyze(newRecipe);
	}

	public RecipeSnapshot ChangeStepAction(
		Recipe recipe,
		int stepIndex,
		ActionDefinition newAction,
		IReadOnlyList<PropertyDefinition> properties)
	{
		if (stepIndex < 0 || stepIndex >= recipe.Steps.Count)
		{
			throw new IndexOutOfRangeException(
				$"Index {stepIndex} is out of range for recipe with {recipe.Steps.Count} steps.");
		}

		var newRecipe = mutator.ChangeStepAction(recipe, stepIndex, newAction, properties);
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
		if (stepIndex < 0 || stepIndex >= recipe.Steps.Count)
		{
			throw new IndexOutOfRangeException(
				$"Index {stepIndex} is out of range for recipe with {recipe.Steps.Count} steps.");
		}

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
}
