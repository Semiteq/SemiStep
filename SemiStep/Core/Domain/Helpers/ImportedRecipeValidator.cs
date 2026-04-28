using FluentResults;

using TypesShared.Config;
using TypesShared.Core;

namespace Domain.Helpers;

internal sealed class ImportedRecipeValidator(
	ConfigRegistry configRegistry)
{
	public Result Validate(Recipe recipe)
	{
		var errors = new List<string>();

		for (var stepIndex = 0; stepIndex < recipe.Steps.Count; stepIndex++)
		{
			var step = recipe.Steps[stepIndex];
			var stepErrors = ValidateStep(step);
			var stepNumber = stepIndex + 1;

			foreach (var error in stepErrors)
			{
				errors.Add($"Step {stepNumber}: {error}");
			}
		}

		return errors.Count == 0
			? Result.Ok()
			: Result.Fail(errors);
	}

	private List<string> ValidateStep(Step step)
	{
		var errors = new List<string>();

		var actionResult = configRegistry.GetAction(step.ActionKey);
		if (actionResult.IsFailed)
		{
			errors.Add($"Unknown action ID {step.ActionKey}");
			return errors;
		}

		var action = actionResult.Value;

		foreach (var column in action.Properties)
		{
			if (column.GroupName is null)
			{
				continue;
			}

			var propertyId = new PropertyId(column.Key);
			if (!step.Properties.TryGetValue(propertyId, out var propertyValue))
			{
				continue;
			}

			if (propertyValue.Value is not int intKey)
			{
				errors.Add($"Group property '{column.Key}' must be integer, got {propertyValue.Type}");
				continue;
			}

			if (configRegistry.GroupHasIntKey(intKey, column.GroupName).IsFailed)
			{
				errors.Add(
					$"Value {intKey} is not a valid member of group '{column.GroupName}' for column '{column.Key}'");
			}
		}

		return errors;
	}
}
