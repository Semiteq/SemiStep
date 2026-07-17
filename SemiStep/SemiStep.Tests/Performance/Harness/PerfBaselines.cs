using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace SemiStep.Tests.Performance.Harness;

// No xunit dependency by design: a probe turns a non-Passed result into an assertion failure, keeping
// the comparer pure.
public sealed record BaselineComparison(bool Passed, string Message);

// Thrown at load when the committed baselines file violates a structural invariant that no re-baseline may
// silently repair (a budget below its own baseline value). Distinct from a compare failure, which is data
// drift and surfaces through BaselineComparison.
public sealed class BaselineConfigException : Exception
{
	public BaselineConfigException(string message)
		: base(message)
	{
	}
}

// Loads Docs/perf/baselines.json and gates measured metrics against it; value/tolerance vs hand-set
// budget: see Docs/perf/README.md, "Baseline vs budget split".
public sealed class PerfBaselines
{
	// For a metric measured but absent from the committed file; budget stays null so the probe fails
	// until a human sets it.
	private const double DefaultTolerancePct = 20.0;

	private const string BaselinesRelativePath = "Docs/perf/baselines.json";

	// The exact commands a failing probe prints so re-baselining is copy-paste. The concrete <pid> path is
	// printed by PerfActualsFixture on the failing run; the pattern here names where to look.
	private const string CaptureAndCopyGuidance =
		"To capture and promote a fresh baseline:\n"
		+ "  1. dotnet run --project SemiStep/SemiStep.Tests/SemiStep.Tests.csproj -c Release -- -explicit only\n"
		+ "  2. Copy-Item \"$env:TEMP\\semistep-perf-actuals-<pid>.json\" \"Docs/perf/baselines.json\"\n"
		+ "     (the failing run prints the concrete <pid> actuals path)";

	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	private readonly Dictionary<string, Metric> _metrics;

	private PerfBaselines(Dictionary<string, Metric> metrics)
	{
		_metrics = metrics;
	}

	public static PerfBaselines Load()
	{
		var path = ResolveBaselinesPath();
		return Parse(File.ReadAllText(path));
	}

	// Lets a probe pick hard-assert vs record-only (see PerfMetricGate).
	public bool Contains(string metricName)
	{
		return _metrics.ContainsKey(metricName);
	}

	public IReadOnlyCollection<string> MetricNames => _metrics.Keys;

	public static PerfBaselines Parse(string json)
	{
		var document = JsonSerializer.Deserialize<BaselinesDocument>(json, _jsonOptions)
			?? new BaselinesDocument();

		var metrics = new Dictionary<string, Metric>(StringComparer.Ordinal);
		foreach (var (name, dto) in document.Metrics ?? new Dictionary<string, MetricDto>())
		{
			if (dto.Budget is double budget && budget < dto.Value)
			{
				throw new BaselineConfigException(
					$"Metric '{name}' has budget {Format(budget)} below its baseline value {Format(dto.Value)}. "
					+ "A budget is the absolute cap and must be >= value; fix Docs/perf/baselines.json by hand.");
			}

			metrics[name] = new Metric(dto.Value, dto.TolerancePct, dto.Budget);
		}

		return new PerfBaselines(metrics);
	}

	public BaselineComparison Compare(string metricName, double actual)
	{
		if (!_metrics.TryGetValue(metricName, out var metric))
		{
			return new BaselineComparison(
				false,
				$"Metric '{metricName}' is not present in Docs/perf/baselines.json. {CaptureAndCopyGuidance}");
		}

		if (metric.Budget is not double budget)
		{
			return new BaselineComparison(
				false,
				$"Metric '{metricName}' has no budget set (baseline value {Format(metric.Value)}). "
				+ "You must set the budget by hand in Docs/perf/baselines.json: round up generously from the "
				+ "value (~1.5-2x) or derive it from an acceptance criterion.");
		}

		if (actual > budget)
		{
			return new BaselineComparison(
				false,
				$"Metric '{metricName}' over budget: actual={Format(actual)} exceeds hard budget {Format(budget)} "
				+ $"(baseline value={Format(metric.Value)}). {CaptureAndCopyGuidance}");
		}

		var upperLimit = metric.Value * (1.0 + (metric.TolerancePct / 100.0));
		if (actual > upperLimit)
		{
			return new BaselineComparison(
				false,
				$"Metric '{metricName}' regressed: actual={Format(actual)} exceeds baseline {Format(metric.Value)} "
				+ $"+{Format(metric.TolerancePct)}% (limit {Format(upperLimit)}). {CaptureAndCopyGuidance}");
		}

		var lowerLimit = metric.Value * (1.0 - (metric.TolerancePct / 100.0));
		if (actual < lowerLimit)
		{
			return new BaselineComparison(
				true,
				$"Metric '{metricName}' improved past tolerance: actual={Format(actual)} is under baseline "
				+ $"{Format(metric.Value)} -{Format(metric.TolerancePct)}% (limit {Format(lowerLimit)}). "
				+ "The baseline is stale; consider re-baselining down.");
		}

		return new BaselineComparison(
			true,
			$"Metric '{metricName}' within tolerance: actual={Format(actual)}, baseline={Format(metric.Value)}, "
			+ $"tolerance={Format(metric.TolerancePct)}%, budget={Format(budget)}.");
	}

	// Budget is emitted even when null so the schema stays identical and a Copy-Item promotion is always
	// safe; unmeasured metrics and budgets carry through verbatim.
	public static string MergeIntoBaselines(
		string currentBaselinesJson,
		IReadOnlyDictionary<string, double> measured,
		string capturedUtc)
	{
		var document = JsonSerializer.Deserialize<BaselinesDocument>(currentBaselinesJson, _jsonOptions)
			?? new BaselinesDocument();

		var context = document.Context ?? new ContextDto();
		context.CapturedUtc = capturedUtc;

		var merged = new SortedDictionary<string, MetricDto>(StringComparer.Ordinal);
		foreach (var (name, dto) in document.Metrics ?? new Dictionary<string, MetricDto>())
		{
			merged[name] = new MetricDto
			{
				Value = dto.Value,
				TolerancePct = dto.TolerancePct,
				Budget = dto.Budget
			};
		}

		foreach (var (name, value) in measured)
		{
			if (merged.TryGetValue(name, out var existing))
			{
				merged[name] = new MetricDto
				{
					Value = value,
					TolerancePct = existing.TolerancePct,
					Budget = existing.Budget
				};
			}
			else
			{
				merged[name] = new MetricDto
				{
					Value = value,
					TolerancePct = DefaultTolerancePct,
					Budget = null
				};
			}
		}

		var result = new BaselinesDocument
		{
			Context = context,
			Metrics = new Dictionary<string, MetricDto>(merged, StringComparer.Ordinal)
		};

		return JsonSerializer.Serialize(result, _jsonOptions);
	}

	// Reads the committed baselines file, or null when it cannot be resolved (so a caller can still emit an
	// actuals artifact against an empty document instead of throwing on assembly disposal).
	public static string? TryReadBaselinesJson()
	{
		try
		{
			return File.ReadAllText(ResolveBaselinesPath());
		}
		catch (FileNotFoundException)
		{
			return null;
		}
		catch (DirectoryNotFoundException)
		{
			return null;
		}
	}

	public static string ResolveBaselinesPath()
	{
		var searched = new List<string>();
		var root = PerfRepoRoot.Find(searched);
		if (root is null)
		{
			throw new FileNotFoundException(
				$"Could not locate the repo root (marker: a '.git' entry or a repo-root global.json). Searched:\n"
				+ $"  {string.Join("\n  ", searched)}\n{CaptureAndCopyGuidance}");
		}

		var path = Path.Combine(root, BaselinesRelativePath.Replace('/', Path.DirectorySeparatorChar));
		if (!File.Exists(path))
		{
			throw new FileNotFoundException(
				$"Repo root found at '{root}' but the baselines file is missing. Expected:\n  {path}\n"
				+ CaptureAndCopyGuidance);
		}

		return path;
	}

	private static string Format(double value)
	{
		return value.ToString("0.######", CultureInfo.InvariantCulture);
	}

	private sealed record Metric(double Value, double TolerancePct, double? Budget);

	private sealed class BaselinesDocument
	{
		public ContextDto? Context { get; set; }

		public Dictionary<string, MetricDto>? Metrics { get; set; }
	}

	private sealed class ContextDto
	{
		public string? Runtime { get; set; }

		public string? Avalonia { get; set; }

		public string? Os { get; set; }

		public string? Testbed { get; set; }

		public string? CapturedUtc { get; set; }
	}

	private sealed class MetricDto
	{
		public double Value { get; set; }

		public double TolerancePct { get; set; }

		public double? Budget { get; set; }
	}
}
