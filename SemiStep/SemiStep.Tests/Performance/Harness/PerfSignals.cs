namespace SemiStep.Tests.Performance.Harness;

// Framework-boundary signals captured around a workload. No SemiStep type names appear here by design:
// every field is a runtime/visual-tree fact, so a gate built on it survives any refactor of the panel
// implementation. FreshVisualInstances is the headline invariant (reference-identity set-diff of the
// snapshot scope's descendants); a scrolled viewport that recycles containers reports 0. The retained
// floor is NOT here: retention is a two-point delta the retention probe samples directly via
// PerfScenarioRunner.SampleRetainedFloorAsync, so a single MeasureAsync window never computes it.
public sealed record PerfSignals(
	long AllocatedBytes,
	int FreshVisualInstances,
	int Gen0);
