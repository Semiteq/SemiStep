using Avalonia.Controls;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Lazy display/editor slot for a ComboBox cell. Renders a lightweight display TextBlock showing the
// selected item's text by default and builds the full ComboBox only when the edit coordinator enters
// edit here, opening the dropdown so the second press behaves like clicking a live combo. The ComboBox is
// the heaviest single cell control, so keeping it out of the resident tree until edit removes the bulk of
// the realized-column cost (a not-in-edit column now instantiates zero ComboBoxes). Writeback stays owned
// by the ComboBox's SelectionChanged, so there is no deferred content to flush on commit/recycle.
internal sealed class TransposedComboCellPresenter : TransposedLazyCellPresenter
{
	public TransposedComboCellPresenter(
		TransposedTextEditCoordinator coordinator,
		Control display,
		Func<Control> editorBuilder)
		: base(coordinator, display, editorBuilder)
	{
	}

	// The combo carries no seeded text; opening the dropdown on entry matches the click-to-open affordance
	// of a live combo (the second press / F2 gesture reads as "open this combo").
	protected override void OnEnteredEdit(Control editor, string? initialText)
	{
		if (editor is ComboBox comboBox)
		{
			comboBox.IsDropDownOpen = true;
		}
	}

	// SelectionChanged already wrote the selection back as the user picked, so there is nothing to flush.
	// The commit-before-rebind / recycle path swaps the ComboBox out for the display without a blur, so the
	// dropdown would stay open on the pooled ComboBox: close it here (mirroring CloseActiveEditor) so a
	// detached popup cannot orphan and a later re-edit's IsDropDownOpen=true actually reopens it.
	protected override void CommitEditorContent(Control editor)
	{
		if (editor is ComboBox comboBox)
		{
			comboBox.IsDropDownOpen = false;
		}
	}

	// The dropdown popup lives in its own visual root; opening it blurs the ComboBox. Keep editing while
	// the dropdown is open so the popup interaction is not torn down, and revert to the display only when
	// focus leaves the closed combo.
	protected override bool ShouldExitOnEditorLostFocus(Control editor)
	{
		return editor is not ComboBox { IsDropDownOpen: true };
	}
}
