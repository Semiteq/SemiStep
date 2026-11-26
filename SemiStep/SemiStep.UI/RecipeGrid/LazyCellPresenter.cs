using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace SemiStep.UI.RecipeGrid;

/// <summary>A grid cell that shows a display control and builds its editor only on edit entry.</summary>
internal abstract class LazyCellPresenter : Border
{
	private readonly CellEditCoordinator _coordinator;
	private readonly Control _display;
	private readonly Func<Control> _editorBuilder;

	protected LazyCellPresenter(CellEditCoordinator coordinator, Control display, Func<Control> editorBuilder)
	{
		_coordinator = coordinator;
		_display = display;
		_editorBuilder = editorBuilder;
		Background = Brushes.Transparent;
		Child = _display;
	}

	public bool IsEditing { get; private set; }

	public Control? Editor { get; private set; }

	public bool CanEnterEdit => IsEffectivelyEnabled;

	// Synchronous so the paired TextInput of a printable keystroke lands on the now-focused editor.
	public void EnterEdit(string? initialText)
	{
		if (!CanEnterEdit)
		{
			return;
		}

		EnsureEditorBuilt();
		Child = Editor;
		IsEditing = true;
		Editor!.Focus();
		OnEnteredEdit(Editor, initialText);
	}

	public void FocusEditor()
	{
		Editor?.Focus();
	}

	public void CommitEdit()
	{
		FinishEdit(returnFocus: false);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		// A recycle detaches while editing; commit before reuse so a rebind can't overwrite pending state.
		FinishEdit(returnFocus: true);

		base.OnDetachedFromVisualTree(e);
	}

	// A recycled DataGrid row keeps its cells attached and only swaps the row: end the edit before the editor
	// shows another row.
	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		if (change.Property == DataContextProperty && IsEditing)
		{
			FinishEdit(returnFocus: true);
		}

		base.OnPropertyChanged(change);
	}

	protected abstract void OnEnteredEdit(Control editor, string? initialText);

	protected abstract void CommitEditorContent(Control editor);

	// Called when an edit ends while its editor held the keyboard, so the grid does not lose focus.
	protected virtual void ReturnFocus()
	{
	}

	protected virtual bool EditorHoldsKeyboard(Control editor)
	{
		return editor.IsKeyboardFocusWithin;
	}

	protected void EndEditReturningFocus()
	{
		FinishEdit(returnFocus: true);
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

	private void FinishEdit(bool returnFocus)
	{
		if (!IsEditing || Editor is null)
		{
			return;
		}

		var editorHeldKeyboard = EditorHoldsKeyboard(Editor);
		CommitEditorContent(Editor);
		ShowDisplay();
		_coordinator.NotifyEditEnded(this);
		if (returnFocus && editorHeldKeyboard)
		{
			ReturnFocus();
		}
	}

	private void EnsureEditorBuilt()
	{
		if (Editor is not null)
		{
			return;
		}

		Editor = _editorBuilder();
		Editor.AddHandler(LostFocusEvent, OnEditorLostFocus, RoutingStrategies.Bubble);
	}

	private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
	{
		if (Editor is null || !ShouldExitOnEditorLostFocus(Editor))
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
}
