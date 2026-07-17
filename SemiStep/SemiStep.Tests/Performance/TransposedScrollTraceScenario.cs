using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using SemiStep.Core.Plc.State;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.RecipeGrid.Transposed;

using Xunit;

namespace SemiStep.Tests.Performance;

// Agent-runnable cpu-trace scenario for the transposed-grid recycle work. NOT part of the normal suite.
// It drives the REAL headless transposed view (production TransposedColumnsPanel item panel) through a
// FIXED workload so the acceptance metric is ABSOLUTE inclusive time for the same work, captured before
// the child-recycle fix (Task 0 baseline) and after it (Task 4). Fixed iteration counts, NOT a fixed
// duration: a fixed-duration loop would let faster code do more iterations and erase the A/B signal.
//
// Run it directly under dotnet-trace (child-launch mode, Release test build), env-gated so the normal
// suite skips it. PowerShell (the child inherits the parent environment):
//   $env:SEMISTEP_TRACE_SCENARIO='1'
//   dotnet-trace collect --format Speedscope -o before.speedscope.json -- \
//     SemiStep/Artifacts/bin/SemiStep.Tests/release/SemiStep.Tests.exe \
//     -method "*TransposedScrollTraceScenario*"
//
// The three phases mirror what the app actually does to the panel:
//   (a) viewport jumps  — 300 round-trips between two columns 200 apart; each jump recycles a full
//       viewport of containers, which is the scroll-phase rebuild the fix targets.
//   (b) change-step-quantity — append then remove 50 steps, the panel churn on step-count edits.
//   (c) execution-tick sweep — walk IsCurrentStep/IsPastStep across 200 steps (RecipeActive ticks),
//       exercising the binding traffic that hits idle subtrees once the child is kept alive.
//
// The host re-attach count (fresh TransposedColumnCellsHost instances built across a scripted scroll) is
// asserted separately by TransposedViewAllocationProbe.Report_HostReattach_IsZeroAfterFix.
[Trait("Category", "Performance")]
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
public sealed class TransposedScrollTraceScenario
{
	private const string ConfigName = "WideParams";
	private const int WindowWidth = 1400;

	// ~2100 columns is the real-scale recipe the user reports. Seeding is O(N^2) in the surface
	// (per-append loop-depth rescan), so this is the slowest single step; if it proves prohibitive the
	// scenario records the largest N that still keeps the traced run >= 20s (see the plan's Task 0 note).
	private const int ScenarioColumns = 2100;

	// Fixed workload counts. Tuned so the untraced baseline run is >= 20s wall-clock on current code.
	private const int ViewportJumpRoundTrips = 300;
	private const int ScrollLowColumn = 950;
	private const int ScrollHighColumn = 1150;
	private const int AddRemoveSteps = 50;
	private const int ExecutionTickSteps = 200;

	private readonly ITestOutputHelper _output;

	public TransposedScrollTraceScenario(ITestOutputHelper output)
	{
		_output = output;
	}

	[AvaloniaFact]
	public async Task Drive_FixedWorkload_ForCpuTrace()
	{
		Assert.SkipUnless(
			Environment.GetEnvironmentVariable("SEMISTEP_TRACE_SCENARIO") == "1",
			"Trace scenario: set SEMISTEP_TRACE_SCENARIO=1 to run.");

		var fixture = new UIFixture();
		await fixture.InitializeAsync(ConfigName);
		try
		{
			var seedStopwatch = Stopwatch.StartNew();
			fixture.SeedRecipe(ScenarioColumns);
			seedStopwatch.Stop();

			var surface = fixture.CreateTransposedSurface();
			surface.Initialize();
			var view = new TransposedRecipeGridView { DataContext = surface };
			var window = new Window { Width = WindowWidth, Height = 800, Content = view };
			window.Show();
			Dispatcher.UIThread.RunJobs();

			var listBox = view.FindControl<ListBox>("StepListBox")!;

			// Warm the JIT/template paths once so the traced phases are steady-state.
			listBox.ScrollIntoView(ScrollHighColumn);
			Dispatcher.UIThread.RunJobs();
			listBox.ScrollIntoView(ScrollLowColumn);
			Dispatcher.UIThread.RunJobs();

			var workloadStopwatch = Stopwatch.StartNew();

			RunViewportJumps(listBox);
			RunChangeStepQuantity(fixture);
			RunExecutionTickSweep(fixture, surface);

			workloadStopwatch.Stop();

			window.Close();

			var report =
				$"seed({ScenarioColumns}) = {seedStopwatch.Elapsed.TotalSeconds:F1}s  " +
				$"workload = {workloadStopwatch.Elapsed.TotalSeconds:F1}s  " +
				$"(jumps={ViewportJumpRoundTrips} addRemove={AddRemoveSteps} ticks={ExecutionTickSteps})";
			_output.WriteLine(report);
			File.WriteAllText(Path.Combine(Path.GetTempPath(), "semistep_trace_scenario.txt"), report);
		}
		finally
		{
			await fixture.DisposeAsync();
		}
	}

	private static void RunViewportJumps(ListBox listBox)
	{
		for (var i = 0; i < ViewportJumpRoundTrips; i++)
		{
			listBox.ScrollIntoView(ScrollHighColumn);
			Dispatcher.UIThread.RunJobs();

			listBox.ScrollIntoView(ScrollLowColumn);
			Dispatcher.UIThread.RunJobs();
		}
	}

	private static void RunChangeStepQuantity(UIFixture fixture)
	{
		var startCount = fixture.Coordinator.CurrentRecipe.StepCount;

		for (var i = 0; i < AddRemoveSteps; i++)
		{
			fixture.Coordinator.AppendStep(RecipeTestDriver.WaitActionId);
			Dispatcher.UIThread.RunJobs();
		}

		for (var i = 0; i < AddRemoveSteps; i++)
		{
			fixture.Coordinator.RemoveStep(startCount + AddRemoveSteps - 1 - i);
			Dispatcher.UIThread.RunJobs();
		}
	}

	private static void RunExecutionTickSweep(UIFixture fixture, TransposedRecipeGridSurface surface)
	{
		var lineCount = Math.Min(ExecutionTickSteps, surface.StepColumns.Count);

		for (var line = 0; line < lineCount; line++)
		{
			fixture.S7Service.PushExecutionState(
				PlcExecutionInfo.Empty with { RecipeActive = true, ActualLine = line });
			Dispatcher.UIThread.RunJobs();
		}

		fixture.S7Service.PushExecutionState(PlcExecutionInfo.Empty with { RecipeActive = false });
		Dispatcher.UIThread.RunJobs();
	}
}
