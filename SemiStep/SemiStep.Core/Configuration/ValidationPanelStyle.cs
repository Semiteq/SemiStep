namespace SemiStep.Core.Configuration;

public sealed record ValidationPanelStyle(
	StyleColor Background,
	StyleColor Foreground,
	StyleColor ErrorColor,
	StyleColor WarningColor,
	double MaxHeight);
