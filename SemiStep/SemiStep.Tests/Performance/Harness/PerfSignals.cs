namespace SemiStep.Tests.Performance.Harness;

// Runtime/visual-tree facts only: no SemiStep type names by design, so a gate built on these survives
// any refactor of the panel implementation.
public sealed record PerfSignals(
	long AllocatedBytes,
	int FreshVisualInstances,
	int Gen0);
