namespace SemiStep.Core.Configuration;

public sealed record ExecutionPalette(
	string Depth0,
	string Depth1,
	string Depth2,
	string Depth3,
	string Depth0Past,
	string Depth1Past,
	string Depth2Past,
	string Depth3Past,
	string CurrentStepMarker);
