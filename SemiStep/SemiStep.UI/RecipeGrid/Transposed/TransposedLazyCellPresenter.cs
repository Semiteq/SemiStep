using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SemiStep.UI.RecipeGrid.Transposed;

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

	public bool CanEnterEdit => IsEffectivelyEnabled;

	// Synchronous so the paired TextInput of a printable keystroke lands on the now-focused editor.
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
		// A recycle detaches while editing; commit before reuse so a rebind can't overwrite pending state.
		CommitEdit();

		base.OnDetachedFromVisualTree(e);
	}

	protected abstract void OnEnteredEdit(Control editor, string? initialText);

	protected abstract void CommitEditorContent(Control editor);

	// Base no-op: the combo ignores typing (F2/pointer are its gestures).
	protected virtual void OnPrintableKeyDown(KeyEventArgs e)
	{
	}

	// The pure TextInput/IME route: a TextInput with no preceding printable KeyDown.
	protected virtual void OnDisplayTextInputCore(TextInputEventArgs e)
	{
	}

	// A combo with an open dropdown keeps editing (focus moved into the popup's own visual root, not away).
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
