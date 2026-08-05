namespace SemiStep.Core.Configuration;

public sealed record ValidationPanelStyle(
	string Background,
	string Foreground,
	string ErrorColor,
	string WarningColor,
	double MaxHeight);
