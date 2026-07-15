using SemiStep.Core.Recipes;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Performance;

// Manual measurement tool, NOT part of the normal suite (Fact.Explicit). Run it directly:
//   dotnet test SemiStep/SemiStep.Tests/SemiStep.Tests.csproj --filter "FullyQualifiedName~CoreAllocationProbe"
// It reports the transient bytes a single Core append allocates (RecipeSession.Apply ->
// RecipeAnalyzer.Analyze -> TimingCalculator/RecipeSnapshot) at growing recipe sizes. The per-append
// figure scaling with N is the O(N)-per-mutation / O(N^2)-per-build churn that the Core tasks of
// Docs/plans/20260714-transposed-grid-allocation-reduction.md reduce. Re-run it before/after each
// Core task and compare against the recorded baseline. Results are written to
// %TEMP%/semistep_core_probe.txt.
//
// gcdump A/B protocol for the UI tasks (retained heap; needs a running RELEASE app):
//   dotnet-gcdump collect -p <pid> -o <name>.gcdump ; dotnet-gcdump report <name>.gcdump
// Diff type totals (DynamicResourceExpression / StyleClassActivator / StyleInstance /
// CompositionVisual / PropertyTextCellViewModel) between the transposed and canonical grids at
// 200 steps. For the recycling churn gate (Task 7) use dotnet-counters (gen0 count, allocation
// rate) or a dotnet-trace GC-events capture while scrolling/adding, not gcdump.
[Trait("Category", "Performance")]
[Trait("Component", "Core")]
public sealed class CoreAllocationProbe
{
	private const int WarmupSize = 50;

	[Fact]
	public async Task Report_PerAppend_CoreAllocation()
	{
		Assert.SkipUnless(
			Environment.GetEnvironmentVariable("SEMISTEP_PROBE") == "1",
			"Measurement probe: set SEMISTEP_PROBE=1 to run.");

		var (_, session, _) = await CoreTestHelper.BuildAsync("WithGroups");

		// Warm the JIT and analysis paths so the measured samples are steady-state.
		MeasureSingleAppend(session, WarmupSize);

		var sizes = new[] { 10, 100, 500 };
		var lines = new List<string>();
		foreach (var size in sizes)
		{
			var bytes = MeasureSingleAppend(session, size);
			Assert.True(bytes > 0, $"expected a positive per-append allocation at N={size}");
			lines.Add($"N={size,4}  per-append = {bytes,12:N0} bytes");
		}

		var report = string.Join(Environment.NewLine, lines);
		var path = Path.Combine(Path.GetTempPath(), "semistep_core_probe.txt");
		File.WriteAllText(path, report);
	}

	// Resets the session, grows the recipe to seedSize steps, then measures the thread-local bytes
	// allocated by exactly one more append at recipe size == seedSize.
	private static long MeasureSingleAppend(RecipeSession session, int seedSize)
	{
		session.Reset().EnsureSuccess("probe reset");
		for (var i = 0; i < seedSize; i++)
		{
			session.AppendStep(RecipeTestDriver.WaitActionId).EnsureSuccess("probe seed append");
		}

		var before = GC.GetAllocatedBytesForCurrentThread();
		session.AppendStep(RecipeTestDriver.WaitActionId).EnsureSuccess("probe measured append");
		var after = GC.GetAllocatedBytesForCurrentThread();

		return after - before;
	}
}
