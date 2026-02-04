using Core.Entities;

using Shared.Entities;

namespace Core.Services;

public sealed class RecipeMutator(StepFactory stepFactory)
{
	public Recipe AddStep(Recipe recipe, ActionDefinition action, IReadOnlyList<PropertyDefinition> properties)
	{
		var step = stepFactory.Create(action, properties);
		return recipe with { Steps = recipe.Steps.Add(step) };
	}

	public Recipe InsertStep(Recipe recipe, int index, ActionDefinition action, IReadOnlyList<PropertyDefinition> properties)
	{
		var step = stepFactory.Create(action, properties);
		return recipe with { Steps = recipe.Steps.Insert(index, step) };
	}

	public Recipe RemoveStep(Recipe recipe, int index)
	{
		return recipe with { Steps = recipe.Steps.RemoveAt(index) };
	}

	public Recipe UpdateProperty(Recipe recipe, int stepIndex, ColumnId columnId, PropertyValue value)
	{
		var step = recipe.Steps[stepIndex];
		var newProperties = step.Properties.SetItem(columnId, value);
		var newStep = step with { Properties = newProperties };
		return recipe with { Steps = recipe.Steps.SetItem(stepIndex, newStep) };
	}

	public Recipe ChangeStepAction(
		Recipe recipe,
		int stepIndex,
		ActionDefinition newAction,
		IReadOnlyList<PropertyDefinition> properties)
	{
		var newStep = stepFactory.Create(newAction, properties);
		return recipe with { Steps = recipe.Steps.SetItem(stepIndex, newStep) };
	}
}
