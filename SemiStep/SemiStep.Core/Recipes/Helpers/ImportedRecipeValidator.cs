using FluentResults;

using SemiStep.Core.Recipes.Errors;

namespace SemiStep.Core.Recipes.Helpers;

public sealed class ImportedRecipeValidator(
	RecipeMetadataRegistry recipeMetadataRegistry)
{
	public Result Validate(Recipe recipe)
	{
		var errors = new List<IError>();

		for (var stepIndex = 0; stepIndex < recipe.Steps.Count; stepIndex++)
		{
			var step = recipe.Steps[stepIndex];
			var stepErrors = ValidateStep(step);
			var stepNumber = stepIndex + 1;

			foreach (var error in stepErrors)
			{
				errors.Add(new AtStepError(stepNumber, error));
			}
		}

		return errors.Count == 0
			? Result.Ok()
			: Result.Fail(errors);
	}

	private List<IError> ValidateStep(Step step)
	{
		var errors = new List<IError>();

		var actionResult = recipeMetadataRegistry.GetAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			errors.Add(new Error($"Unknown action ID {step.ActionKey}"));
			return errors;
		}

		var action = actionResult.Value;

		foreach (var column in action.Properties)
		{
			var propertyId = new PropertyId(column.Key);
			if (!step.Properties.TryGetValue(propertyId, out var propertyValue))
			{
				continue;
			}

			if (column.GroupName is not null)
			{
				ValidateGroupColumn(column, propertyValue, errors);
				continue;
			}

			ValidatePropertyColumn(column, propertyValue, errors);
		}

		return errors;
	}

	private void ValidateGroupColumn(
		ActionPropertyDefinition column,
		PropertyValue propertyValue,
		List<IError> errors)
	{
		if (propertyValue.Value is not int intKey)
		{
			errors.Add(new AtColumnError(
				column.Key,
				new GroupValueNotIntegerError(propertyValue.Type)));
			return;
		}

		var groupResult = recipeMetadataRegistry.GroupHasIntKey(intKey, column.GroupName!);
		if (groupResult.IsFailed)
		{
			errors.Add(new AtColumnError(column.Key, groupResult.Errors[0]));
		}
	}

	private void ValidatePropertyColumn(
		ActionPropertyDefinition column,
		PropertyValue propertyValue,
		List<IError> errors)
	{
		var propertyDefResult = recipeMetadataRegistry.GetProperty(column.PropertyTypeId);
		if (propertyDefResult.IsFailed)
		{
			errors.Add(new AtColumnError(column.Key, propertyDefResult.Errors[0]));
			return;
		}

		var validationResult = PropertyValidator.Validate(propertyDefResult.Value, propertyValue.Value);
		if (validationResult.IsFailed)
		{
			foreach (var error in validationResult.Errors)
			{
				errors.Add(new AtColumnError(column.Key, error));
			}
		}
	}
}
