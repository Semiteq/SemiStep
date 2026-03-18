using FluentResults;

using Shared.Core;
using Shared.ServiceContracts;

namespace Tests.Helpers;

public sealed class StubCsvClipboardService : ICsvClipboardService
{
	public string SerializeSteps(Recipe recipe)
	{
		throw new NotSupportedException("StubCsvClipboardService does not support serialization.");
	}

	public Result<Recipe> DeserializeSteps(string csvBody)
	{
		throw new NotSupportedException("StubCsvClipboardService does not support deserialization.");
	}
}
