using SemiStep.UI.Coordinator;

namespace SemiStep.Tests.UI.Helpers;

// Test sink used inside AvaloniaFact tests where the Mutated handler is always
// invoked on the headless dispatcher thread. The backing List is intentionally
// non-thread-safe — callers outside the single-dispatcher model must not use this type.
public sealed class RecordingRecipeSink
{
	private readonly List<MutationSignal> _signals = new();

	public IReadOnlyList<MutationSignal> Signals => _signals;

	public void OnMutation(MutationSignal signal)
	{
		_signals.Add(signal);
	}
}
