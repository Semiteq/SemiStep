using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using SemiStep.Core.Recipes;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.Performance.Harness;

using Xunit;

namespace SemiStep.Tests.Performance;

// Core per-append allocation gate. No driver: the Core API (RecipeSession.Apply -> RecipeAnalyzer.Analyze
// -> TimingCalculator/RecipeSnapshot) is already black-box, so this probe measures it directly and adopts
// only the baseline-compare mechanism (report to the actuals fixture + assert-or-record against
// Docs/perf/baselines.json).
//
// Explicit measurement fact: plain `dotnet test`/CI does not run it (xunit v3 Explicit). Run:
//   SemiStep/Artifacts/bin/SemiStep.Tests/<config>/SemiStep.Tests.exe \
//     -explicit only -method "*CoreAllocationProbe*"
//
// It reports the transient bytes a single Core append allocates at growing recipe sizes. The per-append
// figure scaling with N is the O(N)-per-mutation / O(N^2)-per-build churn that the Core allocation-reduction
// work targets. Results are also written to %TEMP%/semistep_core_probe.txt.
[Trait("Category", "Performance")]
[Trait("Component", "Core")]
public sealed class CoreAllocationProbe
{
	private const int WarmupSize = 50;

	private static readonly int[] _sizes = { 10, 100, 500 };

	private readonly ITestOutputHelper _output;
	private readonly PerfActualsFixture _actuals;

	public CoreAllocationProbe(ITestOutputHelper output, PerfActualsFixture actuals)
	{
		_output = output;
		_actuals = actuals;
	}

	[Fact(Explicit = true)]
	public async Task Report_PerAppend_CoreAllocation()
	{
		var (_, session, _) = await CoreTestHelper.BuildAsync("WithGroups");

		// Warm the JIT and analysis paths so the measured samples are steady-state.
		MeasureSingleAppend(session, WarmupSize);

		var baselines = PerfBaselines.Load();
		var lines = new List<string>();
		foreach (var size in _sizes)
		{
			var bytes = MeasureSingleAppend(session, size);
			Assert.True(bytes > 0, $"expected a positive per-append allocation at N={size}");
			lines.Add($"N={size,4}  per-append = {bytes,12:N0} bytes");
			PerfMetricGate.AssertOrRecord(
				_actuals, baselines, _output, PerfMetricNames.CorePerAppendBytes(size), bytes);
		}

		var report = string.Join(Environment.NewLine, lines);
		_output.WriteLine(report);
		File.WriteAllText(Path.Combine(Path.GetTempPath(), "semistep_core_probe.txt"), report);
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
