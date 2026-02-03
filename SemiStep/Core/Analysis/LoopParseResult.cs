using Shared.Reasons;

namespace Core.Analysis;

public sealed record LoopParseResult(IReadOnlyList<LoopInfo> Loops, IReadOnlyList<AbstractReason> Reasons);
