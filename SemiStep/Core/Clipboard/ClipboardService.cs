using FluentResults;

using TypesShared.Core;

namespace ClipBoard;

public sealed class ClipboardService
{
	private readonly ClipboardSerializer _serializer;

	internal ClipboardService(ClipboardSerializer serializer)
	{
		_serializer = serializer;
	}

	public string SerializeSteps(Recipe recipe)
	{
		return _serializer.SerializeSteps(recipe);
	}

	public Result<Recipe> DeserializeSteps(string tsvBody)
	{
		return _serializer.DeserializeSteps(tsvBody);
	}
}
