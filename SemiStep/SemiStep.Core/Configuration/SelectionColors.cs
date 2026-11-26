namespace SemiStep.Core.Configuration;

/// <summary>The selection accent and its blend weight (0..1) over the cell colour.</summary>
public sealed record SelectionColors(
	StyleColor Accent,
	double Weight);
