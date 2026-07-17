using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SemiStep.Tests.Performance.Harness;

// xunit v3 assembly fixture: a thread-safe collector each probe reports measured metrics to. On assembly
// disposal it writes %TEMP%/semistep-perf-actuals-<pid>.json ONCE: the PROPOSED NEXT baselines.json. The
// PID suffix keeps concurrent test processes from clobbering each other.
public sealed class PerfActualsFixture : IDisposable
{
	private readonly ConcurrentDictionary<string, double> _measured = new(StringComparer.Ordinal);

	private int _disposed;

	// Live read-only view, not a snapshot.
	public IReadOnlyDictionary<string, double> CollectedMetrics => _measured;

	// Last write wins for a repeated name.
	public void Report(string metricName, double value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(metricName);
		_measured[metricName] = value;
	}

	public static string DefaultOutputPath()
	{
		return Path.Combine(Path.GetTempPath(), $"semistep-perf-actuals-{Environment.ProcessId}.json");
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}

		if (_measured.IsEmpty)
		{
			return;
		}

		var currentBaselinesJson = PerfBaselines.TryReadBaselinesJson() ?? EmptyBaselinesDocument;
		var proposed = PerfBaselines.MergeIntoBaselines(currentBaselinesJson, _measured, DateTime.UtcNow.ToString("O"));
		var outputPath = DefaultOutputPath();
		File.WriteAllText(outputPath, proposed);
		Console.WriteLine($"[perf] proposed baselines written: {outputPath}");
	}

	private const string EmptyBaselinesDocument =
		"{ \"context\": { \"runtime\": null, \"avalonia\": null, \"os\": null, \"testbed\": null, "
		+ "\"capturedUtc\": null }, \"metrics\": {} }";
}
