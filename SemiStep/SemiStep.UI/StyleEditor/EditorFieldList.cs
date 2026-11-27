using SemiStep.UI.Localization;

namespace SemiStep.UI.StyleEditor;

/// <summary>The editor's validated number fields and its colour drafts, each in page order.</summary>
internal static class EditorFieldList
{
	private const decimal Percent = 100;

	public static IReadOnlyList<EditorField> Build(GridStyleEditorViewModel editor)
	{
		var sections = editor.Sections;

		return
		[
			new EditorField(sections.Fonts, Resources.EditorGridHeaders, editor.Fonts.HeaderFontSize),
			new EditorField(sections.Fonts, Resources.EditorGridCells, editor.Fonts.CellFontSize),
			new EditorField(sections.Fonts, Resources.EditorPageStatusBar, editor.StatusBar.FontSize),

			new EditorField(sections.Grid, Resources.EditorRowHeight, editor.Layout.RowHeight),
			new EditorField(sections.Grid, Resources.EditorPaddingLeft, editor.Layout.CellPaddingLeft),
			new EditorField(sections.Grid, Resources.EditorPaddingTop, editor.Layout.CellPaddingTop),
			new EditorField(sections.Grid, Resources.EditorPaddingRight, editor.Layout.CellPaddingRight),
			new EditorField(sections.Grid, Resources.EditorPaddingBottom, editor.Layout.CellPaddingBottom),

			new EditorField(sections.CellColors, Resources.EditorOpacity, editor.Selection.Weight, Percent),

			new EditorField(sections.Highlight, Resources.EditorCurrentStep, editor.Execution.RunningWeight, Percent),
			new EditorField(sections.Highlight, Resources.EditorCompletedSteps, editor.Execution.PastWeight, Percent),
			new EditorField(sections.Highlight, Resources.EditorDepth1, editor.Loop.Depth1Weight, Percent),
			new EditorField(sections.Highlight, Resources.EditorDepth2, editor.Loop.Depth2Weight, Percent),
			new EditorField(sections.Highlight, Resources.EditorDepth3, editor.Loop.Depth3Weight, Percent),

			new EditorField(sections.StatusBar, Resources.EditorVerticalPadding, editor.StatusBar.Padding),

			new EditorField(sections.NotificationPanel, Resources.EditorMaximumHeight, editor.ValidationPanel.MaxHeight)
		];
	}

	public static IReadOnlyList<ColorDraft> ColorDrafts(GridStyleEditorViewModel editor)
	{
		return
		[
			editor.Chrome.GridBackground,
			editor.Chrome.GridBorder,
			editor.Chrome.GridLine,
			editor.Chrome.HeaderForeground,

			editor.ChangedCells.Changed,
			editor.GreyCells.ReadOnly,
			editor.GreyCells.Disabled,
			editor.Selection.Accent,

			editor.Execution.Running,
			editor.Loop.Color,

			editor.StatusBar.Background,
			editor.StatusBar.Foreground,
			editor.Chrome.SecondaryForeground,
			editor.Chrome.Connected,
			editor.Chrome.Disconnected,
			editor.Chrome.Separator,
			editor.Chrome.SubtleBorder,

			editor.ValidationPanel.Background,
			editor.Chrome.PanelHeaderBackground,
			editor.ValidationPanel.Foreground,
			editor.ValidationPanel.ErrorColor,
			editor.ValidationPanel.WarningColor
		];
	}
}
