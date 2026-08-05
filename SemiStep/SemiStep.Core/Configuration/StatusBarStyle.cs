namespace SemiStep.Core.Configuration;

public sealed record StatusBarStyle(
	string Background,
	string Foreground,
	double Padding,
	double ItemSpacing,
	int FontSize,
	int Weight,
	bool Italic,
	int TimerLabelFontSize,
	int TimerLabelWeight,
	bool TimerLabelItalic,
	int TimerValueFontSize,
	int TimerValueWeight,
	bool TimerValueItalic);
