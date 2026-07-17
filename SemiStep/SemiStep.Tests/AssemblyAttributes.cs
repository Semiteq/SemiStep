using SemiStep.Tests.Performance.Harness;

using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

// Assembly-wide actuals collector for the explicit performance probes. Constructed once per test process
// and disposed at assembly teardown; on a normal `dotnet test` run the explicit probes do not execute, so
// nothing is reported and the empty-guard in Dispose writes no artifact (CI stays clean).
[assembly: AssemblyFixture(typeof(PerfActualsFixture))]
