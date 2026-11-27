using System.Globalization;

using SemiStep.UI.Localization;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// One validated number field: its page, its label and the message the footer shows while it is invalid;
/// <paramref name="shownScale"/> turns the stored range into the one the box shows.
/// </summary>
internal sealed class EditorField(StyleEditorSection section, string label, NumberDraft draft, decimal shownScale = 1)
{
	public StyleEditorSection Section { get; } = section;

	public NumberDraft Draft { get; } = draft;

	public bool IsValid => Draft.IsValid;

	public string Message =>
		string.Format(CultureInfo.CurrentCulture, Resources.EditorFieldMessage, Section.Title, label, Problem);

	private string Problem =>
		string.Format(
			CultureInfo.CurrentCulture,
			Resources.EditorEnterNumber,
			Draft.Minimum * shownScale,
			Draft.Maximum * shownScale);
}
