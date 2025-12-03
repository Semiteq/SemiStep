using FluentResults;

namespace Core.Analyzer;

/// <summary>
/// Result of structure validation phase.
/// </summary>
public sealed record StructureResult(IReadOnlyList<IReason> Reasons);
