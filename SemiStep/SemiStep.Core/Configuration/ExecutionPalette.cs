namespace SemiStep.Core.Configuration;

public sealed record ExecutionPalette(
	StyleColor Depth0,
	StyleColor Depth1,
	StyleColor Depth2,
	StyleColor Depth3,
	StyleColor Depth0Past,
	StyleColor Depth1Past,
	StyleColor Depth2Past,
	StyleColor Depth3Past,
	StyleColor CurrentStepMarker);
