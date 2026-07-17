using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using FluentAssertions;

using Xunit;

namespace SemiStep.Tests.Performance.Harness;

[Trait("Category", "Unit")]
[Trait("Component", "UI")]
[Trait("Area", "Performance")]
public sealed class PerfBaselinesTests
{
	private const string OneMetricJson = """
	{
	  "context": { "runtime": "net10", "avalonia": "12.0.5", "os": "win-x64", "testbed": "dev-primary", "capturedUtc": null },
	  "metrics": {
	    "scroll.bytes": { "value": 1000, "tolerancePct": 20, "budget": 2000 }
	  }
	}
	""";

	// Budget sits inside the tolerance band (1000 +20% = 1200, budget 1100): the hard cap bites before the
	// soft tolerance does.
	private const string TightBudgetJson = """
	{
	  "context": { "runtime": "net10", "avalonia": "12.0.5", "os": "win-x64", "testbed": "dev-primary", "capturedUtc": null },
	  "metrics": {
	    "scroll.bytes": { "value": 1000, "tolerancePct": 20, "budget": 1100 }
	  }
	}
	""";

	private const string NullBudgetJson = """
	{
	  "context": { "runtime": "net10", "avalonia": "12.0.5", "os": "win-x64", "testbed": "dev-primary", "capturedUtc": null },
	  "metrics": {
	    "scroll.bytes": { "value": 1000, "tolerancePct": 20, "budget": null }
	  }
	}
	""";

	private const string BudgetBelowValueJson = """
	{
	  "context": { "runtime": "net10", "avalonia": "12.0.5", "os": "win-x64", "testbed": "dev-primary", "capturedUtc": null },
	  "metrics": {
	    "scroll.bytes": { "value": 1000, "tolerancePct": 20, "budget": 500 }
	  }
	}
	""";

	// Two committed metrics: one budgeted, one with a null budget. Used to prove the merge carries both
	// budget shapes through untouched.
	private const string TwoMetricJson = """
	{
	  "context": { "runtime": "net10", "avalonia": "12.0.5", "os": "win-x64", "testbed": "dev-primary", "capturedUtc": "2020-01-01T00:00:00Z" },
	  "metrics": {
	    "scroll.bytes": { "value": 1000, "tolerancePct": 20, "budget": 2000 },
	    "add.bytes": { "value": 500, "tolerancePct": 10, "budget": null }
	  }
	}
	""";

	private const string EmptyMetricsJson = """
	{
	  "context": { "runtime": "net10", "avalonia": "12.0.5", "os": "win-x64", "testbed": "dev-primary", "capturedUtc": null },
	  "metrics": {}
	}
	""";

	[Fact]
	public void Compare_WithinTolerance_Passes()
	{
		var baselines = PerfBaselines.Parse(OneMetricJson);

		var comparison = baselines.Compare("scroll.bytes", 1100);

		comparison.Passed.Should().BeTrue();
	}

	[Fact]
	public void Contains_ReturnsTrue_WhenMetricPresent()
	{
		var baselines = PerfBaselines.Parse(OneMetricJson);

		baselines.Contains("scroll.bytes").Should().BeTrue();
	}

	[Fact]
	public void Contains_ReturnsFalse_WhenMetricAbsent()
	{
		var baselines = PerfBaselines.Parse(OneMetricJson);

		baselines.Contains("absent.metric").Should().BeFalse();
	}

	// Reads the real committed file on purpose: the file<->constants drift is exactly what it guards.
	[Fact]
	public void CommittedBaselines_MetricNames_MatchProbeMetricConstants()
	{
		var baselines = PerfBaselines.Load();

		baselines.MetricNames.Should().BeEquivalentTo(
			PerfMetricNames.All,
			"the committed baselines.json must carry exactly the metrics the probes report; a mismatch means a "
			+ "probe metric was renamed/dropped or a baseline entry drifted, silently disabling a gate");
	}

	[Fact]
	public void Compare_OverTolerance_Fails_NamingMetricBaselineAndActual()
	{
		var baselines = PerfBaselines.Parse(OneMetricJson);

		var comparison = baselines.Compare("scroll.bytes", 1300);

		comparison.Passed.Should().BeFalse();
		comparison.Message.Should().Contain("scroll.bytes");
		comparison.Message.Should().Contain("1000");
		comparison.Message.Should().Contain("1300");
	}

	[Fact]
	public void Compare_ImprovementBeyondTolerance_Passes_WithStaleBaselineAdvisory()
	{
		var baselines = PerfBaselines.Parse(OneMetricJson);

		var comparison = baselines.Compare("scroll.bytes", 500);

		comparison.Passed.Should().BeTrue();
		comparison.Message.Should().Contain("stale");
	}

	[Fact]
	public void Compare_OverBudget_Fails_EvenWithinBaselineTolerance()
	{
		var baselines = PerfBaselines.Parse(TightBudgetJson);

		var comparison = baselines.Compare("scroll.bytes", 1150);

		comparison.Passed.Should().BeFalse();
		comparison.Message.Should().Contain("budget");
		comparison.Message.Should().Contain("1100");
	}

	[Fact]
	public void Parse_BudgetBelowValue_RejectedAtLoad()
	{
		var act = () => PerfBaselines.Parse(BudgetBelowValueJson);

		act.Should().Throw<BaselineConfigException>()
			.WithMessage("*scroll.bytes*");
	}

	[Fact]
	public void Compare_MissingMetric_Fails_WithCaptureAndCopyGuidance()
	{
		var baselines = PerfBaselines.Parse(OneMetricJson);

		var comparison = baselines.Compare("absent.metric", 5);

		comparison.Passed.Should().BeFalse();
		comparison.Message.Should().Contain("absent.metric");
		comparison.Message.Should().Contain(
			"dotnet run --project SemiStep/SemiStep.Tests/SemiStep.Tests.csproj -c Release -- -explicit only");
		comparison.Message.Should().Contain("Copy-Item");
	}

	[Fact]
	public void Compare_NullBudget_Fails_WithSetBudgetByHandGuidance()
	{
		var baselines = PerfBaselines.Parse(NullBudgetJson);

		var comparison = baselines.Compare("scroll.bytes", 900);

		comparison.Passed.Should().BeFalse();
		comparison.Message.Should().Contain("set the budget by hand");
	}

	[Fact]
	public void Merge_OverlaysMeasured_CarriesUnmeasuredAndBudgetsThrough_WritesAbsentBudgetAsNull()
	{
		var measured = new Dictionary<string, double>
		{
			["scroll.bytes"] = 1234,
			["new.metric"] = 4200
		};

		var proposed = PerfBaselines.MergeIntoBaselines(TwoMetricJson, measured, "2026-01-01T00:00:00Z");

		using var document = JsonDocument.Parse(proposed);
		var root = document.RootElement;
		var metrics = root.GetProperty("metrics");

		var scroll = metrics.GetProperty("scroll.bytes");
		scroll.GetProperty("value").GetDouble().Should().Be(1234);
		scroll.GetProperty("tolerancePct").GetDouble().Should().Be(20);
		scroll.GetProperty("budget").GetDouble().Should().Be(2000);

		var add = metrics.GetProperty("add.bytes");
		add.GetProperty("value").GetDouble().Should().Be(500);
		add.GetProperty("tolerancePct").GetDouble().Should().Be(10);
		add.GetProperty("budget").ValueKind.Should().Be(JsonValueKind.Null);

		var fresh = metrics.GetProperty("new.metric");
		fresh.GetProperty("value").GetDouble().Should().Be(4200);
		fresh.TryGetProperty("budget", out var freshBudget).Should().BeTrue();
		freshBudget.ValueKind.Should().Be(JsonValueKind.Null);

		var context = root.GetProperty("context");
		context.GetProperty("testbed").GetString().Should().Be("dev-primary");
		context.GetProperty("capturedUtc").GetString().Should().Be("2026-01-01T00:00:00Z");
	}

	// The assembly runs serially (parallelization disabled), so probes do not actually report concurrently;
	// this exercises the collector's defensive thread-safety anyway - it must stay correct if that changes.
	[Fact]
	public async Task ConcurrentReports_AreThreadSafe_AllLandInTheArtifact()
	{
		const int ClassCount = 8;
		const int MetricsPerClass = 50;
		var fixture = new PerfActualsFixture();

		var reporters = new List<Task>();
		for (var classIndex = 0; classIndex < ClassCount; classIndex++)
		{
			var localClassIndex = classIndex;
			reporters.Add(Task.Run(
				() =>
				{
					for (var metricIndex = 0; metricIndex < MetricsPerClass; metricIndex++)
					{
						fixture.Report(
							$"class{localClassIndex}.metric{metricIndex}",
							(localClassIndex * 1000) + metricIndex);
					}
				},
				TestContext.Current.CancellationToken));
		}

		await Task.WhenAll(reporters);

		fixture.CollectedMetrics.Should().HaveCount(ClassCount * MetricsPerClass);

		var proposed = PerfBaselines.MergeIntoBaselines(
			EmptyMetricsJson,
			fixture.CollectedMetrics,
			"2026-01-01T00:00:00Z");

		using var document = JsonDocument.Parse(proposed);
		var metrics = document.RootElement.GetProperty("metrics");

		for (var classIndex = 0; classIndex < ClassCount; classIndex++)
		{
			for (var metricIndex = 0; metricIndex < MetricsPerClass; metricIndex++)
			{
				metrics.TryGetProperty($"class{classIndex}.metric{metricIndex}", out _)
					.Should().BeTrue($"class{classIndex}.metric{metricIndex} should be present");
			}
		}
	}
}
