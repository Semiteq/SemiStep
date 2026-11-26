using FluentResults;

using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Formulas;

namespace SemiStep.Core.Recipes;

public sealed class ActionDefinition(
	int id,
	string uiName,
	DeployDuration deployDuration,
	IReadOnlyList<ActionPropertyDefinition> properties,
	FormulaDefinition? formula = null,
	ActionRole role = ActionRole.Action)
{
	public int Id { get; } = id;

	public string UiName { get; } = uiName;

	public DeployDuration DeployDuration { get; } = deployDuration;

	public ActionRole Role { get; } = role;

	public IReadOnlyList<ActionPropertyDefinition> Properties { get; } = properties;

	public FormulaDefinition? Formula { get; } = formula;

	/// <summary>A step of this action opens a loop, so its row shows the loop pass pill while the loop runs.</summary>
	public bool StartsLoop => Id == (int)ServiceActionId.ForLoop;

	public Result<ActionPropertyDefinition> FindProperty(string propertyKey)
	{
		var property = Properties.FirstOrDefault(c => c.Key == propertyKey);

		return property is null
			? (Result<ActionPropertyDefinition>)Result.Fail(
				$"Property '{propertyKey}' is not defined in action '{UiName}'")
			: (Result<ActionPropertyDefinition>)property;
	}
}
