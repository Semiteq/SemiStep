using FluentResults;

using TypesShared.Core;
using TypesShared.Domain;

namespace ClipBoard;

internal sealed class ClipboardService(ClipboardSerializer serializer) : IClipboardService
{
	public string SerializeSteps(Recipe recipe)
	{
		return serializer.SerializeSteps(recipe);
	}

	public Result<Recipe> DeserializeSteps(string tsvBody)
	{
		return serializer.DeserializeSteps(tsvBody);
	}
}
