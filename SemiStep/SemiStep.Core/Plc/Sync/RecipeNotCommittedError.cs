using FluentResults;

namespace SemiStep.Core.Plc.Sync;

public sealed class RecipeNotCommittedError()
	: Error("Recipe not committed on PLC")
{
}
