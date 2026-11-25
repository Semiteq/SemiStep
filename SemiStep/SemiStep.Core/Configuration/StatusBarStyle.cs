namespace SemiStep.Core.Configuration;

public sealed record StatusBarStyle(
	StyleColor Background,
	StyleColor Foreground,
	double Padding,
	int FontSize,
	int Weight,
	bool Italic);
