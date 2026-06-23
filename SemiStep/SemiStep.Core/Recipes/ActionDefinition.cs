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
		FormulaDefinition? formula = null,
		ActionRole role = ActionRole.Action)
	{
		Id = id;
		UiName = uiName;
		DeployDuration = deployDuration;
		Properties = properties;
		Formula = formula;
		Role = role;
	}

	public int Id { get; }

	public string UiName { get; }

	public DeployDuration DeployDuration { get; }

	public ActionRole Role { get; }

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
