using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class GroupNotFoundError(string groupId)
	: Error($"Group '{groupId}' not found")
{
	public string GroupId { get; } = groupId;
}
