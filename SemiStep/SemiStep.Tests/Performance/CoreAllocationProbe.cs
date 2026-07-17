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

// Core per-append allocation gate. No driver: the Core API is already black-box, so this probe measures
// it directly and adopts only the baseline-compare mechanism. Per-append bytes scaling with N is the
// O(N)-per-mutation churn signal.
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
