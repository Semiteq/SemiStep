using System.Collections.Immutable;

namespace SemiStep.UI.Coordinator;

public abstract record MutationSignal
{
	public sealed record StepAppended(int Index) : MutationSignal;

	public sealed record StepsInserted(int StartIndex, int Count) : MutationSignal;

	public sealed record StepRemoved(int RemovedIndex) : MutationSignal;

	public sealed record StepsRemoved(ImmutableArray<int> RemovedIndices) : MutationSignal;

	public sealed record StepActionChanged(int StepIndex) : MutationSignal;

	public sealed record PropertyUpdated(int StepIndex) : MutationSignal;

	public sealed record RecipeReplaced : MutationSignal;
}
