using FluentResults;

using SemiStep.Core.Recipes.Errors;

namespace SemiStep.Core.Recipes;

internal sealed class StepMutationValidator(RecipeMetadataRegistry recipeMetadataRegistry)
{
	private readonly RecipeMetadataRegistry _recipeMetadataRegistry = recipeMetadataRegistry;

	public static Result CheckStepIndex(Recipe recipe, int index)
	{
		return index < 0 || index >= recipe.Steps.Count
			? Result.Fail(new StepIndexOutOfRangeError(index, recipe.Steps.Count))
			: Result.Ok();
	}

	public static Result CheckInsertIndex(Recipe recipe, int index)
	{
		return index < 0 || index > recipe.Steps.Count
			? Result.Fail(new InsertIndexOutOfRangeError(index, recipe.Steps.Count))
			: Result.Ok();
	}

	public Result<ActionDefinition> ResolveStepAction(Recipe recipe, int stepIndex)
	{
		var indexCheck = CheckStepIndex(recipe, stepIndex);
		return indexCheck.IsFailed
			? indexCheck.ToResult<ActionDefinition>()
			: _recipeMetadataRegistry.GetAction(recipe.Steps[stepIndex].ActionKey);
	}

	public Result<PropertyValue> ParseColumnValue(ActionDefinition action, string columnKey, string value)
	{
		var actionPropertyResult = action.FindProperty(columnKey);
		if (actionPropertyResult.IsFailed)
		{
			return actionPropertyResult.ToResult<PropertyValue>();
		}

		var actionProperty = actionPropertyResult.Value;

		var propertyDefinitionResult = _recipeMetadataRegistry.GetProperty(actionProperty.PropertyTypeId);
		if (propertyDefinitionResult.IsFailed)
		{
			return propertyDefinitionResult.ToResult<PropertyValue>();
		}

		var parseResult = PropertyParser.Parse(value, propertyDefinitionResult.Value);
		if (parseResult.IsFailed)
		{
			return parseResult;
		}

		var parsedValue = parseResult.Value;

		var typeCheck = PropertyValidator.Validate(propertyDefinitionResult.Value, parsedValue.Value);
		if (typeCheck.IsFailed)
		{
			return typeCheck.ToResult<PropertyValue>();
		}

		var groupCheck = PropertyValidator.ValidateGroupValue(actionProperty, parsedValue, _recipeMetadataRegistry);
		return groupCheck.IsFailed ? groupCheck.ToResult<PropertyValue>() : Result.Ok(parsedValue);
	}
}
