namespace SemiStep.Core.Configuration;

public sealed record GridStyleFonts(
	string FontFamily,
	int HeaderFontSize,
	int HeaderFontWeight,
	bool HeaderItalic,
	int CellFontSize,
	int CellFontWeight,
	bool CellItalic);
