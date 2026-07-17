using System;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using Xunit;

namespace SemiStep.Tests.Performance.Harness;

[Trait("Category", "Unit")]
[Trait("Component", "UI")]
[Trait("Area", "Performance")]
public sealed class PerfScenarioRunnerTests
{
	// The no-op workload only allocates whatever a single idle Dispatcher.RunJobs costs, orders of
	// magnitude below the deliberate 8 MiB array workload; this ceiling separates the two without
	// tracking exact idle overhead.
	private const long NearZeroBytesCeiling = 500_000;

	private const int WorkloadArrayBytes = 8 * 1024 * 1024;

	[AvaloniaFact]
	public async Task NoOpWorkload_ReportsZeroFreshVisuals_AndNearZeroBytes()
	{
		var scope = new StackPanel();
		scope.Children.Add(new TextBlock { Text = "seed" });
		var window = ShowWindow(scope);

		var runner = new PerfScenarioRunner();
		var signals = await runner.MeasureAsync(scope, NoOp, NoOp);

		signals.FreshVisualInstances.Should().Be(0);
		signals.AllocatedBytes.Should().BeLessThan(NearZeroBytesCeiling);

		window.Close();
	}

	[AvaloniaFact]
	public async Task WorkloadAddingChildInsideScope_ReportsOneFreshVisual()
	{
		var scope = new StackPanel();
		var window = ShowWindow(scope);

		var runner = new PerfScenarioRunner();
		var signals = await runner.MeasureAsync(
			scope,
			NoOp,
			() =>
			{
				scope.Children.Add(new TextBlock { Text = "added" });
				return Task.CompletedTask;
			});

		signals.FreshVisualInstances.Should().Be(1);

		window.Close();
	}

	[AvaloniaFact]
	public async Task WorkloadAddingChildOutsideScope_ReportsZeroFreshVisuals()
	{
		var scope = new StackPanel();
		var sibling = new StackPanel();
		var root = new StackPanel();
		root.Children.Add(scope);
		root.Children.Add(sibling);
		var window = ShowWindow(root);

		var runner = new PerfScenarioRunner();
		var signals = await runner.MeasureAsync(
			scope,
			NoOp,
			() =>
			{
				sibling.Children.Add(new TextBlock { Text = "added" });
				return Task.CompletedTask;
			});

		signals.FreshVisualInstances.Should().Be(0);

		window.Close();
	}

	[AvaloniaFact]
	public async Task WorkloadAllocatingArray_ReportsAtLeastThatManyBytes()
	{
		var scope = new StackPanel();
		var window = ShowWindow(scope);

		var runner = new PerfScenarioRunner();
		var signals = await runner.MeasureAsync(
			scope,
			NoOp,
			() =>
			{
				var payload = new byte[WorkloadArrayBytes];
				GC.KeepAlive(payload);
				return Task.CompletedTask;
			});

		signals.AllocatedBytes.Should().BeGreaterThanOrEqualTo(WorkloadArrayBytes);

		window.Close();
	}

	[AvaloniaFact]
	public async Task ThrowingWorkload_Propagates_AndLeavesRunnerUsableForNextMeasurement()
	{
		var scope = new StackPanel();
		scope.Children.Add(new TextBlock { Text = "seed" });
		var window = ShowWindow(scope);

		var runner = new PerfScenarioRunner();

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => runner.MeasureAsync(
				scope,
				NoOp,
				() => throw new InvalidOperationException("workload boom")));

		var signals = await runner.MeasureAsync(scope, NoOp, NoOp);
		signals.FreshVisualInstances.Should().Be(0);
		signals.AllocatedBytes.Should().BeLessThan(NearZeroBytesCeiling);

		window.Close();
	}

	[AvaloniaFact]
	public async Task SampleRetainedFloor_ReturnsPositiveAndStableFloor()
	{
		var runner = new PerfScenarioRunner();

		var first = await runner.SampleRetainedFloorAsync();
		var second = await runner.SampleRetainedFloorAsync();

		first.Should().BeGreaterThan(0);
		second.Should().BeGreaterThan(0);

		var drift = Math.Abs(second - first) / (double)first;
		drift.Should().BeLessThan(
			0.25,
			"two back-to-back floor samples with no workload between them must be stable within GC noise");
	}

	private static Task NoOp()
	{
		return Task.CompletedTask;
	}

	private static Window ShowWindow(Control content)
	{
		var window = new Window { Width = 400, Height = 400, Content = content };
		window.Show();
		Dispatcher.UIThread.RunJobs();
		return window;
	}
}
