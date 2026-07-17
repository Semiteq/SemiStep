using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SemiStep.Tests.Performance.Harness;

// snapshotScope MUST be the items-panel subtree, never the TopLevel: window chrome (scrollbars, focus
// adorners) adds stray visuals that make the FreshVisualInstances == 0 invariant flaky.
//
// warmup MUST reach steady-state peak realization (scroll the full measured range once) so the recycle
// pool is pre-filled; otherwise the first workload pass legitimately creates containers and == 0 is
// unachievable. Workload is fixed (iteration counts), never fixed duration.
public sealed class PerfScenarioRunner
{
	public async Task<PerfSignals> MeasureAsync(Visual snapshotScope, Func<Task> warmup, Func<Task> workload)
	{
		await warmup();
		SettleAndCollect();

		var snapshot = new HashSet<Visual>(snapshotScope.GetVisualDescendants(), ReferenceEqualityComparer.Instance);

		var gen0Before = GC.CollectionCount(0);
		var bytesBefore = GC.GetAllocatedBytesForCurrentThread();

		await workload();
		Dispatcher.UIThread.RunJobs();

		var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
		var gen0 = GC.CollectionCount(0) - gen0Before;

		var freshVisualInstances = snapshotScope
			.GetVisualDescendants()
			.Count(descendant => !snapshot.Contains(descendant));

		return new PerfSignals(allocatedBytes, freshVisualInstances, gen0);
	}

	// Standalone floor sample for the two-point retention gate: the retention invariant is a DELTA
	// (floor before N cycles vs floor after), and a single MeasureAsync window cannot produce it.
	public Task<long> SampleRetainedFloorAsync()
	{
		Dispatcher.UIThread.RunJobs();
		FullBlockingCollect();
		return Task.FromResult(GC.GetTotalMemory(forceFullCollection: true));
	}

	private static void SettleAndCollect()
	{
		Dispatcher.UIThread.RunJobs();
		FullBlockingCollect();
	}

	private static void FullBlockingCollect()
	{
		GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
		GC.WaitForPendingFinalizers();
		GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
		GC.WaitForPendingFinalizers();
	}
}
