using Recipe.Entities;
using Recipe.Exceptions;

using Shared.Entities;

namespace Recipe.Services;

public sealed class RecipeMutator(StepFactory stepFactory)
{
	public Entities.Recipe AddStep(Entities.Recipe recipe, ActionDefinition action, IReadOnlyList<PropertyDefinition> properties)
	{
		var step = stepFactory.Create(action, properties);
		return recipe with { Steps = recipe.Steps.Add(step) };
	}

	public Entities.Recipe InsertStep(Entities.Recipe recipe, int index, ActionDefinition action, IReadOnlyList<PropertyDefinition> properties)
	{
		ValidateInsertIndex(recipe, index);
		var step = stepFactory.Create(action, properties);
		return recipe with { Steps = recipe.Steps.Insert(index, step) };
	}

	public Entities.Recipe RemoveStep(Entities.Recipe recipe, int index)
	{
		ValidateIndex(recipe, index);
		return recipe with { Steps = recipe.Steps.RemoveAt(index) };
	}

	public Entities.Recipe UpdateProperty(Entities.Recipe recipe, int stepIndex, ColumnId columnId, PropertyValue value)
	{
		ValidateIndex(recipe, stepIndex);
		var step = recipe.Steps[stepIndex];
		var newProperties = step.Properties.SetItem(columnId, value);
		var newStep = step with { Properties = newProperties };
		return recipe with { Steps = recipe.Steps.SetItem(stepIndex, newStep) };
	}

	public Entities.Recipe ChangeStepAction(
		Entities.Recipe recipe,
		int stepIndex,
		ActionDefinition newAction,
		IReadOnlyList<PropertyDefinition> properties)
	{
		ValidateIndex(recipe, stepIndex);
		var newStep = stepFactory.Create(newAction, properties);
		return recipe with { Steps = recipe.Steps.SetItem(stepIndex, newStep) };
	}

	private static void ValidateIndex(Entities.Recipe recipe, int index)
	{
		if (index < 0 || index >= recipe.Steps.Count)
		{
			throw new InvalidStepIndexException(index, recipe.Steps.Count);
		}
	}

	private static void ValidateInsertIndex(Entities.Recipe recipe, int index)
	{
		if (index < 0 || index > recipe.Steps.Count)
		{
			throw new InvalidStepIndexException(index, recipe.Steps.Count);
		}
	}
}
