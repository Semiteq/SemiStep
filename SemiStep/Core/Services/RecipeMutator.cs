using Core.Entities;
using Core.Exceptions;

namespace Core.Services;

public sealed class RecipeMutator
{
	private readonly StepFactory _stepFactory;

	public RecipeMutator(StepFactory stepFactory)
	{
		_stepFactory = stepFactory;
	}

	public Recipe AddStep(Recipe recipe, string actionKey)
	{
		var step = _stepFactory.Create(actionKey);
		return recipe with { Steps = recipe.Steps.Add(step) };
	}

	public Recipe InsertStep(Recipe recipe, int index, string actionKey)
	{
		ValidateInsertIndex(recipe, index);
		var step = _stepFactory.Create(actionKey);
		return recipe with { Steps = recipe.Steps.Insert(index, step) };
	}

	public Recipe RemoveStep(Recipe recipe, int index)
	{
		ValidateIndex(recipe, index);
		return recipe with { Steps = recipe.Steps.RemoveAt(index) };
	}

	public Recipe UpdateProperty(Recipe recipe, int stepIndex, ColumnId column, PropertyValue value)
	{
		ValidateIndex(recipe, stepIndex);
		var step = recipe.Steps[stepIndex];
		var newProperties = step.Properties.SetItem(column, value);
		var newStep = step with { Properties = newProperties };
		return recipe with { Steps = recipe.Steps.SetItem(stepIndex, newStep) };
	}

	private static void ValidateIndex(Recipe recipe, int index)
	{
		if (index < 0 || index >= recipe.Steps.Count)
			throw new InvalidStepIndexException(index, recipe.Steps.Count);
	}

	private static void ValidateInsertIndex(Recipe recipe, int index)
	{
		if (index < 0 || index > recipe.Steps.Count)
			throw new InvalidStepIndexException(index, recipe.Steps.Count);
	}
}
