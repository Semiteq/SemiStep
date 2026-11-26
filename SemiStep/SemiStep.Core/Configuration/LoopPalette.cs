namespace SemiStep.Core.Configuration;

/// <summary>The loop tint and its blend weights (0..1) per nesting depth; a deeper loop uses depth 3.</summary>
public sealed record LoopPalette(
	StyleColor Color,
	double Depth1Weight,
	double Depth2Weight,
	double Depth3Weight);
