using ReactiveUI;

using SemiStep.UI.Localization;

namespace SemiStep.UI.StyleEditor;

/// <summary>The six pages of the style editor, in the order the section list shows them.</summary>
public sealed class StyleEditorSections
{
	public StyleEditorSections()
	{
		All = [Fonts, Grid, CellColors, Highlight, StatusBar, NotificationPanel];
	}

	public StyleEditorSection Fonts { get; } = new(Resources.EditorPageFonts);

	public StyleEditorSection Grid { get; } = new(Resources.EditorPageGrid);

	public StyleEditorSection CellColors { get; } = new(Resources.EditorPageCellColors);

	public StyleEditorSection Highlight { get; } = new(Resources.EditorPageHighlight);

	public StyleEditorSection StatusBar { get; } = new(Resources.EditorPageStatusBar);

	public StyleEditorSection NotificationPanel { get; } = new(Resources.EditorPageNotificationPanel);

	public IReadOnlyList<StyleEditorSection> All { get; }
}

/// <summary>One row of the section list; <see cref="HasError"/> is true while a field on its page is invalid.</summary>
public sealed class StyleEditorSection(string title) : ReactiveObject
{
	public string Title { get; } = title;

	public bool HasError
	{
		get;
		set => this.RaiseAndSetIfChanged(ref field, value);
	}
}
