using FluentResults;

using SemiStep.Core.Recipes.Formulas;

namespace SemiStep.Core.Recipes;

public sealed record ActionDefinition(
	int Id,
	string UiName,
	DeployDuration DeployDuration,
	IReadOnlyList<ActionPropertyDefinition> Properties,
	FormulaDefinition? Formula = null)
{
	public Result<ActionPropertyDefinition> FindProperty(string propertyKey)
	{
		var property = Properties.FirstOrDefault(c => c.Key == propertyKey);

		if (property is null)
		{
			return Result.Fail(
				$"Property '{propertyKey}' is not defined in action '{UiName}'");
		}

		return property;
	}
}
