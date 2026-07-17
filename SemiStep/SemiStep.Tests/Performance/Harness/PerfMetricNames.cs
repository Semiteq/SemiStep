using System.Collections.Generic;

namespace SemiStep.Tests.Performance.Harness;

// Single source of truth for every baseline metric name the probes report. Both the probes and the
// always-on drift guard reference these constants, and the drift guard asserts this set equals the metric
// keys in Docs/perf/baselines.json. That closes the silent-downgrade hole: renaming a metric here (without
// re-capturing the baseline) or dropping an entry from the committed file fails the normal suite instead of
// quietly turning that gate into a record-only no-op.
internal static class PerfMetricNames
{
	public const string TransposedViewportJumpBytesPerColumn = "transposed.viewportJump.bytesPerColumn";
	public const string CanonicalViewportJumpBytesPerColumn = "canonical.viewportJump.bytesPerColumn";
	public const string TransposedPerAddBytesN20 = "transposed.perAdd.bytes.n20";
	public const string TransposedPerAddBytesN120 = "transposed.perAdd.bytes.n120";
	public const string TransposedRetentionFloorBytes = "transposed.retention.floorBytes";
	public const string CanonicalRetentionFloorBytes = "canonical.retention.floorBytes";
	public const string CorePerAppendBytesN10 = "core.perAppend.bytes.n10";
	public const string CorePerAppendBytesN100 = "core.perAppend.bytes.n100";
	public const string CorePerAppendBytesN500 = "core.perAppend.bytes.n500";

	public static readonly IReadOnlyList<string> All = new[]
	{
		TransposedViewportJumpBytesPerColumn,
		CanonicalViewportJumpBytesPerColumn,
		TransposedPerAddBytesN20,
		TransposedPerAddBytesN120,
		TransposedRetentionFloorBytes,
		CanonicalRetentionFloorBytes,
		CorePerAppendBytesN10,
		CorePerAppendBytesN100,
		CorePerAppendBytesN500,
	};

	// The Core probe measures per-append bytes at a swept recipe size; each size maps to a fixed constant so
	// a swept name never drifts from the committed baseline key.
	public static string CorePerAppendBytes(int size)
	{
		return size switch
		{
			10 => CorePerAppendBytesN10,
			100 => CorePerAppendBytesN100,
			500 => CorePerAppendBytesN500,
			_ => throw new KeyNotFoundException(
				$"No core per-append metric constant for recipe size {size}; add one to PerfMetricNames."),
		};
	}
}
