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

// Diagnostics, not a gate: drives the real headless transposed view through a fixed workload for a CPU
// trace and asserts nothing (see Docs/perf/README.md, Diagnostic layer). Fixed iteration counts, never a
// fixed duration: a fixed-duration loop lets faster code do more iterations and erases the A/B signal.
[Trait("Category", "Performance")]
[Trait("Component", "UI")]
[Trait("Area", "RecipeGrid")]
public sealed class TransposedScrollTraceScenario
{
	private const string ConfigName = "WideParams";
	private const int WindowWidth = 1400;

	// ~2100 columns is the real-scale recipe; seeding is O(N^2) in the surface, so the slowest single step.
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

	[AvaloniaFact(Explicit = true)]
	public async Task Drive_FixedWorkload_ForCpuTrace()
	{
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
