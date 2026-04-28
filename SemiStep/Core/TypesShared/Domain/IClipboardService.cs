using FluentResults;

using TypesShared.Core;

namespace TypesShared.Domain;

public interface IClipboardService
{
	string SerializeSteps(Recipe recipe);

	Result<Recipe> DeserializeSteps(string csvBody);
}
