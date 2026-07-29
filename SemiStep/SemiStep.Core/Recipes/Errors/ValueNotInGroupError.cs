using FluentResults;

namespace SemiStep.Core.Recipes.Errors;

public sealed class ValueNotInGroupError(int key, string groupId)
	: Error($"Value {key} is not a valid member of group '{groupId}'")
{
	public int Key { get; } = key;

	public string GroupId { get; } = groupId;
}
