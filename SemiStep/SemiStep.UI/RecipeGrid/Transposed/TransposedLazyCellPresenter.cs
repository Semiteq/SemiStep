using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Shared lazy display/editor slot for a transposed cell. Renders a lightweight display control by default
// and builds the heavy editor (TextBox / ComboBox) only when the edit coordinator enters edit here,
// releasing it back to the display on blur/commit/recycle. This removes the always-live editor weight
// from the fresh-container build and the resident visual tree while preserving the select-then-edit
// gesture, click-and-type, and keyboard-driven edit entry. A transparent background makes the whole cell
// hit-testable so a press anywhere in it enters edit; Focusable makes it an arrow-navigation target from
// which F2 opens the editor. Subclasses supply the per-kind editor build/commit and edit-entry specifics.
internal abstract class TransposedLazyCellPresenter : Border
{
	private readonly TransposedTextEditCoordinator _coordinator;
	private readonly Control _display;
	private readonly Func<Control> _editorBuilder;
	private Control? _editor;

	protected TransposedLazyCellPresenter(
		TransposedTextEditCoordinator coordinator,
		Control display,
		Func<Control> editorBuilder)
	{
		_coordinator = coordinator;
		_display = display;
		_editorBuilder = editorBuilder;
		Background = Brushes.Transparent;
		Focusable = true;
		Child = _display;

		AddHandler(KeyDownEvent, OnDisplayKeyDown, RoutingStrategies.Bubble);
		AddHandler(TextInputEvent, OnDisplayTextInput, RoutingStrategies.Bubble);
	}

	public bool IsEditing { get; private set; }

	public Control? Editor => _editor;

	// Applicability + surface read-only fold into the presenter's bound IsEnabled, so a disabled presenter
	// is non-focusable and blocks edit entry (read-only / inapplicable / read-only-surface all gate here).
	public bool CanEnterEdit => IsEffectivelyEnabled;

	// Swaps the display for the editor, focuses it, and hands off to the subclass for the just-entered
	// state (seed text / open dropdown). Synchronous so the paired TextInput of a printable keystroke
	// lands on the now-focused editor.
	public void EnterEdit(string? initialText)
	{
		if (!IsEffectivelyEnabled)
		{
			return;
		}

		EnsureEditorBuilt();
		Child = _editor;
		IsEditing = true;
		_editor!.Focus();
		OnEnteredEdit(_editor, initialText);
	}

	public void FocusEditor()
	{
		_editor?.Focus();
	}

	// Commit-before-rebind: flush the editor's content to its captured cell (text parses/writes; combo is
	// a no-op) and drop back to the display without moving focus. Called when a pooled presenter is
	// recycled out or rebound to a new column so the edit lands on the cell the user was editing before the
	// display binding rebinds. Idempotent: a second call after ShowDisplay is a no-op.
	public void CommitEdit()
	{
		if (!IsEditing || _editor is null)
		{
			return;
		}

		CommitEditorContent(_editor);
		ShowDisplay();
		_coordinator.NotifyEditEnded(this);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		// A recycle detaches this slot while it may still be editing. Commit/close the editor before the
		// pooled presenter is reused, so a rebind can never silently overwrite pending state.
		CommitEdit();

		base.OnDetachedFromVisualTree(e);
	}

	// Applies the just-entered edit state after the editor is built, swapped in, and focused.
	protected abstract void OnEnteredEdit(Control editor, string? initialText);

	// Pushes the editor's pending content to the model on commit-before-rebind.
	protected abstract void CommitEditorContent(Control editor);

	// A printable KeyDown reached a focused display. Subclasses that accept typed entry override to enter
	// edit (text seeds with replace semantics); the combo ignores typing (F2 / pointer are its gestures), so
	// the base no-op leaves it alone.
	protected virtual void OnPrintableKeyDown(KeyEventArgs e)
	{
	}

	// A TextInput reached a focused display without a preceding printable KeyDown (the pure TextInput /
	// IME route). Subclasses that accept typed entry override to seed the editor with the carried character.
	protected virtual void OnDisplayTextInputCore(TextInputEventArgs e)
	{
	}

	// Whether an editor LostFocus ends the edit. Text always exits; a combo whose dropdown is open keeps
	// editing (focus moved into the popup's own visual root, not away from the cell).
	protected virtual bool ShouldExitOnEditorLostFocus(Control editor)
	{
		return true;
	}

	protected void BeginEdit(string? initialText)
	{
		_coordinator.BeginEdit(this, initialText);
	}

	protected static bool IsPrintable(string? text, KeyModifiers modifiers)
	{
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}

		if ((modifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
		{
			return false;
		}

		return !char.IsControl(text[0]);
	}

	private void EnsureEditorBuilt()
	{
		if (_editor is not null)
		{
			return;
		}

		_editor = _editorBuilder();
		_editor.AddHandler(LostFocusEvent, OnEditorLostFocus, RoutingStrategies.Bubble);
	}

	// The editor blurs on commit (Enter / click-away), on recycle (Avalonia detaches the container), when
	// focus moves into another cell's editor, and (for a combo) when its dropdown opens. Swap back to the
	// display and tell the coordinator the edit ended, unless the subclass keeps editing across this blur.
	private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
	{
		if (_editor is null || !ShouldExitOnEditorLostFocus(_editor))
		{
			return;
		}

		ShowDisplay();
		_coordinator.NotifyEditEnded(this);
	}

	private void ShowDisplay()
	{
		IsEditing = false;
		if (!ReferenceEquals(Child, _display))
		{
			Child = _display;
		}
	}

	private void OnDisplayKeyDown(object? sender, KeyEventArgs e)
	{
		if (IsEditing || !IsEffectivelyEnabled)
		{
			return;
		}

		if (e.Key == Key.F2 && e.KeyModifiers == KeyModifiers.None)
		{
			BeginEdit(initialText: null);
			e.Handled = true;
			return;
		}

		OnPrintableKeyDown(e);
	}

	private void OnDisplayTextInput(object? sender, TextInputEventArgs e)
	{
		if (IsEditing || !IsEffectivelyEnabled)
		{
			return;
		}

		OnDisplayTextInputCore(e);
	}
}
