using FluentResults;

using SemiStep.Core.Recipes.Formulas;

namespace SemiStep.Core.Recipes;

public sealed class ActionDefinition
{
	public ActionDefinition(
		int id,
		string uiName,
		DeployDuration deployDuration,
		IReadOnlyList<ActionPropertyDefinition> properties,
		FormulaDefinition? formula = null)
	{
		Id = id;
		UiName = uiName;
		DeployDuration = deployDuration;
		Properties = properties;
		Formula = formula;
	}

	public int Id { get; }

	public string UiName { get; }

	public DeployDuration DeployDuration { get; }

	public IReadOnlyList<ActionPropertyDefinition> Properties { get; }

	public FormulaDefinition? Formula { get; }

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
