using FluentResults;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes.Formulas;

namespace SemiStep.Core.Recipes;

public sealed class RecipeEditor
{
	private readonly ConfigRegistry _configRegistry;
	private readonly FormulaApplicationCoordinator _formulaCoordinator;
	private readonly PropertyParser _propertyParser;
	private readonly RecipeWorkspace _workspace;

	internal RecipeEditor(
		RecipeWorkspace workspace,
		ConfigRegistry configRegistry,
		FormulaApplicationCoordinator formulaCoordinator,
		PropertyParser propertyParser)
	{
		_workspace = workspace;
		_configRegistry = configRegistry;
		_formulaCoordinator = formulaCoordinator;
		_propertyParser = propertyParser;
	}

	public Result AppendStep(int actionId)
	{
		var actionResult = _configRegistry.GetAction(actionId);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult();
		}

		var step = StepInitializer.Create(actionResult.Value, _configRegistry);
		var newRecipe = _workspace.CurrentRecipe.AppendStep(step);

		return _workspace.Apply(newRecipe);
	}

	public Result InsertStep(int index, int actionId)
	{
		var indexCheck = ValidateInsertIndex(_workspace.CurrentRecipe, index);
		if (indexCheck.IsFailed)
		{
			return indexCheck;
		}

		var actionResult = _configRegistry.GetAction(actionId);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult();
		}

		var step = StepInitializer.Create(actionResult.Value, _configRegistry);
		var newRecipe = _workspace.CurrentRecipe.InsertStep(index, step);

		return _workspace.Apply(newRecipe);
	}

	public Result RemoveStep(int index)
	{
		var indexCheck = ValidateStepIndex(_workspace.CurrentRecipe, index);
		if (indexCheck.IsFailed)
		{
			return indexCheck;
		}

		var newRecipe = _workspace.CurrentRecipe.RemoveStep(index);

		return _workspace.Apply(newRecipe);
	}

	public Result InsertSteps(int startIndex, IReadOnlyList<Step> steps)
	{
		var indexCheck = ValidateInsertIndex(_workspace.CurrentRecipe, startIndex);
		if (indexCheck.IsFailed)
		{
			return indexCheck;
		}

		var newRecipe = _workspace.CurrentRecipe.InsertSteps(startIndex, steps);

		return _workspace.Apply(newRecipe);
	}

	public Result RemoveSteps(IReadOnlyList<int> indices)
	{
		var current = _workspace.CurrentRecipe;
		foreach (var i in indices)
		{
			var indexCheck = ValidateStepIndex(current, i);
			if (indexCheck.IsFailed)
			{
				return indexCheck;
			}
		}

		var newRecipe = current.RemoveSteps(indices);

		return _workspace.Apply(newRecipe);
	}

	public Result ChangeStepAction(int stepIndex, int newActionId)
	{
		var indexCheck = ValidateStepIndex(_workspace.CurrentRecipe, stepIndex);
		if (indexCheck.IsFailed)
		{
			return indexCheck;
		}

		var actionResult = _configRegistry.GetAction(newActionId);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult();
		}

		var step = StepInitializer.Create(actionResult.Value, _configRegistry);
		var newRecipe = _workspace.CurrentRecipe.ReplaceStep(stepIndex, step);

		return _workspace.Apply(newRecipe);
	}

	public Result UpdateStepProperty(int stepIndex, string columnKey, string value)
	{
		var indexCheck = ValidateStepIndex(_workspace.CurrentRecipe, stepIndex);
		if (indexCheck.IsFailed)
		{
			return indexCheck;
		}

		var current = _workspace.CurrentRecipe;
		var step = current.Steps[stepIndex];

		var actionResult = _configRegistry.GetAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			return actionResult.ToResult();
		}

		var action = actionResult.Value;

		var actionPropertyResult = action.FindProperty(columnKey);
		if (actionPropertyResult.IsFailed)
		{
			return actionPropertyResult.ToResult();
		}

		var actionProperty = actionPropertyResult.Value;

		var propertyDefinitionResult = _configRegistry.GetProperty(actionProperty.PropertyTypeId);
		if (propertyDefinitionResult.IsFailed)
		{
			return propertyDefinitionResult.ToResult();
		}

		var parseResult = _propertyParser.Parse(value, propertyDefinitionResult.Value);
		if (parseResult.IsFailed)
		{
			return parseResult.ToResult();
		}

		var parsedValue = parseResult.Value;

		var typeCheck = PropertyValidator.Validate(propertyDefinitionResult.Value, parsedValue.Value);
		if (typeCheck.IsFailed)
		{
			return typeCheck;
		}

		var groupCheck = PropertyValidator.ValidateGroupValue(actionProperty, parsedValue, _configRegistry);
		if (groupCheck.IsFailed)
		{
			return groupCheck;
		}

		var updatedStep = step.WithProperty(columnKey, parsedValue);

		var formulaResult = _formulaCoordinator.ApplyIfExists(
			updatedStep,
			action,
			new PropertyId(columnKey),
			formulaDefinition: null);

		if (formulaResult.IsFailed)
		{
			return formulaResult.ToResult();
		}

		var newRecipe = current.ReplaceStep(stepIndex, formulaResult.Value);

		return _workspace.Apply(newRecipe);
	}

	private static Result ValidateInsertIndex(Recipe recipe, int index)
	{
		if (index < 0 || index > recipe.Steps.Count)
		{
			return Result.Fail($"Insert index {index} is out of range for recipe with {recipe.Steps.Count} steps");
		}

		return Result.Ok();
	}

	private static Result ValidateStepIndex(Recipe recipe, int index)
	{
		if (index < 0 || index >= recipe.Steps.Count)
		{
			return Result.Fail($"Step index {index} is out of range for recipe with {recipe.Steps.Count} steps");
		}

		return Result.Ok();
	}
}
