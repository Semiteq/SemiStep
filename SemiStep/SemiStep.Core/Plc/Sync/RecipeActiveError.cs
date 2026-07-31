using FluentResults;

namespace SemiStep.Core.Plc.Sync;

public sealed class RecipeActiveError()
	: Error("Recipe is being executed on PLC")
{
}
