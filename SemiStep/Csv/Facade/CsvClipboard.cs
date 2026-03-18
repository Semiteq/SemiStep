using Csv.ClipboardService;

using FluentResults;

using Shared.Core;
using Shared.ServiceContracts;

namespace Csv.Facade;

internal sealed class CsvClipboard(CsvClipboardSerializer clipboardSerializer) : ICsvClipboardService
{
	public string SerializeSteps(Recipe recipe)
	{
		return clipboardSerializer.SerializeSteps(recipe);
	}

	public Result<Recipe> DeserializeSteps(string csvBody)
	{
		return clipboardSerializer.DeserializeSteps(csvBody);
	}
}
