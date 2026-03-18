using FluentResults;

using Shared.Core;

namespace Shared.ServiceContracts;

public interface ICsvClipboardService
{
	string SerializeSteps(Recipe recipe);

	Result<Recipe> DeserializeSteps(string csvBody);
}
