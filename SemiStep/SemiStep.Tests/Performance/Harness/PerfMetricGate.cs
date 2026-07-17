using Xunit;

namespace SemiStep.Tests.Performance.Harness;

// Shared baseline-gating helper for the probes, so the assert-or-record logic lives in exactly one place
// and each probe loads the baselines ONCE (pass the loaded instance in) instead of re-reading the file per
// metric. Two modes:
//   - AssertOrRecord: hard-fails on a regression once the metric has a committed baseline; record-only
//     (report + print, no assert) while the baseline is absent.
//   - RecordAdvisory: reports and prints the comparison but NEVER asserts - for telemetry-only metrics
//     (e.g. the absolute retention floor, whose 20% tolerance on a ~100 MB floor would hide a slow leak,
//     so the flat-delta invariant is that probe's hard gate instead).
internal static class PerfMetricGate
{
	public static void AssertOrRecord(
		PerfActualsFixture actuals,
		PerfBaselines baselines,
		ITestOutputHelper output,
		string metricName,
		double actual)
	{
		actuals.Report(metricName, actual);

		if (!baselines.Contains(metricName))
		{
			output.WriteLine(
				$"[perf] {metricName}={actual:N0} recorded (no baseline yet; gates once captured).");
			return;
		}

		var comparison = baselines.Compare(metricName, actual);
		output.WriteLine($"[perf] {comparison.Message}");
		Assert.True(comparison.Passed, comparison.Message);
	}

	public static void RecordAdvisory(
		PerfActualsFixture actuals,
		PerfBaselines baselines,
		ITestOutputHelper output,
		string metricName,
		double actual)
	{
		actuals.Report(metricName, actual);

		if (!baselines.Contains(metricName))
		{
			output.WriteLine(
				$"[perf][advisory] {metricName}={actual:N0} recorded (telemetry only, never gates).");
			return;
		}

		var comparison = baselines.Compare(metricName, actual);
		output.WriteLine($"[perf][advisory] {comparison.Message}");
	}
}
