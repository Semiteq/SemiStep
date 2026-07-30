using FluentResults;

namespace SemiStep.Core.Recipes.Clipboard.Errors;

public sealed class ClipboardParseFailedError()
	: Error("Failed to parse clipboard data")
{
}
