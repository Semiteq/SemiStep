using FluentResults;

namespace SemiStep.Core.Recipes;

public sealed record ActionDefinition(
	int Id,
	string UiName,
	string DeployDuration,
	IReadOnlyList<ActionPropertyDefinition> Properties)
{
	public Result<ActionPropertyDefinition> FindProperty(string propertyKey)
	{
		var property = Properties.FirstOrDefault(c => c.Key == propertyKey);

		if (property is null)
		{
			return Result.Fail<ActionPropertyDefinition>(
				$"Property '{propertyKey}' is not defined in action '{UiName}'");
		}

		return property;
	}
}
