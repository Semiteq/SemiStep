using System.Reactive;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Attached behavior: when the bound <see cref="IObservable{Unit}"/> emits, commits the in-flight cell edit,
/// closes an open combo list and takes focus back from a combo box. Fires when the grid becomes read-only.
/// </summary>
public static class DataGridEditorCloseBehavior
{
	public static readonly AttachedProperty<IObservable<Unit>?> TriggerProperty =
		AvaloniaProperty.RegisterAttached<DataGrid, IObservable<Unit>?>(
			"Trigger",
			typeof(DataGridEditorCloseBehavior));

	private static readonly AttachedProperty<IDisposable?> _subscriptionProperty =
		AvaloniaProperty.RegisterAttached<DataGrid, IDisposable?>(
			"Subscription",
			typeof(DataGridEditorCloseBehavior));

	private static readonly AttachedProperty<bool> _lifecycleHandlersAttachedProperty =
		AvaloniaProperty.RegisterAttached<DataGrid, bool>(
			"LifecycleHandlersAttached",
			typeof(DataGridEditorCloseBehavior));

	static DataGridEditorCloseBehavior()
	{
		TriggerProperty.Changed.AddClassHandler<DataGrid>(OnTriggerChanged);
	}

	public static IObservable<Unit>? GetTrigger(DataGrid dataGrid)
	{
		return dataGrid.GetValue(TriggerProperty);
	}

	public static void SetTrigger(DataGrid dataGrid, IObservable<Unit>? value)
	{
		dataGrid.SetValue(TriggerProperty, value);
	}

	private static void OnTriggerChanged(DataGrid dataGrid, AvaloniaPropertyChangedEventArgs args)
	{
		Subscribe(dataGrid);
		EnsureLifecycleHandlersAttached(dataGrid);
	}

	private static void Subscribe(DataGrid dataGrid)
	{
		Unsubscribe(dataGrid);

		if (GetTrigger(dataGrid) is not { } trigger)
		{
			return;
		}

		var subscription = trigger.Subscribe(_ => CloseEditor(dataGrid));
		dataGrid.SetValue(_subscriptionProperty, subscription);
	}

	private static void Unsubscribe(DataGrid dataGrid)
	{
		dataGrid.GetValue(_subscriptionProperty)?.Dispose();
		dataGrid.SetValue(_subscriptionProperty, null);
	}

	// The host detaches a grid on every orientation flip and attaches it again with the same trigger value, so the
	// subscription follows the visual-tree attachment and not only trigger changes.
	private static void EnsureLifecycleHandlersAttached(DataGrid dataGrid)
	{
		if (dataGrid.GetValue(_lifecycleHandlersAttachedProperty))
		{
			return;
		}

		dataGrid.AttachedToVisualTree += OnAttachedToVisualTree;
		dataGrid.DetachedFromVisualTree += OnDetachedFromVisualTree;
		dataGrid.SetValue(_lifecycleHandlersAttachedProperty, true);
	}

	private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
	{
		if (sender is DataGrid dataGrid && dataGrid.GetValue(_subscriptionProperty) is null)
		{
			Subscribe(dataGrid);
		}
	}

	private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs args)
	{
		if (sender is DataGrid dataGrid)
		{
			Unsubscribe(dataGrid);
		}
	}

	private static void CloseEditor(DataGrid dataGrid)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			CloseEditorOnUiThread(dataGrid);
			return;
		}

		Dispatcher.UIThread.Post(() => CloseEditorOnUiThread(dataGrid));
	}

	private static void CloseEditorOnUiThread(DataGrid dataGrid)
	{
		dataGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
		CloseComboBoxes(dataGrid);
	}

	// Closing a list hands focus back to its combo box, so focus is moved only after every list is closed.
	private static void CloseComboBoxes(DataGrid dataGrid)
	{
		var focusInsideComboBox = OwningComboBox(TopLevel.GetTopLevel(dataGrid)?.FocusManager?.GetFocusedElement())
			is { } focusedComboBox && dataGrid.IsVisualAncestorOf(focusedComboBox);

		foreach (var comboBox in dataGrid.GetVisualDescendants().OfType<ComboBox>())
		{
			if (comboBox.IsDropDownOpen)
			{
				comboBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
			}
		}

		if (focusInsideComboBox)
		{
			dataGrid.Focus();
		}
	}

	// An open list holds focus on one of its items, which live in the popup, outside the grid's visual tree.
	private static ComboBox? OwningComboBox(IInputElement? focused)
	{
		return focused switch
		{
			ComboBox comboBox => comboBox,
			ComboBoxItem item => ItemsControl.ItemsControlFromItemContainer(item) as ComboBox,
			_ => null,
		};
	}
}
