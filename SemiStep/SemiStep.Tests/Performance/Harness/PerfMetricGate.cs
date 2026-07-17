using Xunit;

namespace SemiStep.Tests.Performance.Harness;

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
