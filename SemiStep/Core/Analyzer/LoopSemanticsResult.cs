using Core.Loops;

using FluentResults;

namespace Core.Analyzer;

/// <summary>
/// Loop semantics after resolving iteration counts, depths, and integrity flags.
/// </summary>
public sealed record LoopSemanticsResult(
	IReadOnlyList<LoopNode> Nodes,
	IReadOnlyList<IReason> Reasons,
	bool LoopIntegrityCompromised,
	bool MaxDepthExceeded);
