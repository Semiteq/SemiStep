using Core.Loops;

using FluentResults;

namespace Core.Analyzer;

/// <summary>
/// Raw loop parse output before semantic normalization.
/// </summary>
public sealed record LoopParseResult(
	IReadOnlyList<LoopNode> Nodes,
	IReadOnlyList<IReason> Reasons);
