using System.Collections.Immutable;

using Core.Entities;
using Core.Loops;

using FluentResults;

namespace Core.Snapshot;

/// <summary>
/// Immutable result of a full recipe analysis.
/// </summary>
public sealed record RecipeAnalysisSnapshot(
	Recipe Recipe,
	int StepCount,
	LoopTree LoopTree,
	IReadOnlyDictionary<int, TimeSpan> StepStartTimes,
	TimeSpan TotalDuration,
	IReadOnlyList<IReason> Reasons,
	AnalysisFlags Flags,
	bool IsValid)
{
	public static RecipeAnalysisSnapshot Empty =>
		new(
			Recipe.Empty,
			0,
			LoopTree.Empty,
			ImmutableDictionary<int, TimeSpan>.Empty,
			TimeSpan.Zero,
			ImmutableArray<IReason>.Empty,
			AnalysisFlags.None,
			IsValid: false);
}
