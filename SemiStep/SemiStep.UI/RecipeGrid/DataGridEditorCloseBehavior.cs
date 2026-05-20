using System.Reactive;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Attached behavior: when the bound <see cref="IObservable{Unit}"/> emits a value,
/// commits any in-flight cell edit on the associated <see cref="DataGrid"/>, exiting
/// editing mode. Used to close an open editor when the grid transitions to read-only.
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
		var previousSubscription = dataGrid.GetValue(_subscriptionProperty);
		previousSubscription?.Dispose();

		if (args.NewValue is IObservable<Unit> trigger)
		{
			var subscription = trigger.Subscribe(_ => CloseEditor(dataGrid));
			dataGrid.SetValue(_subscriptionProperty, subscription);
			dataGrid.DetachedFromVisualTree -= OnDetachedFromVisualTree;
			dataGrid.DetachedFromVisualTree += OnDetachedFromVisualTree;
		}
		else
		{
			dataGrid.SetValue(_subscriptionProperty, null);
			dataGrid.DetachedFromVisualTree -= OnDetachedFromVisualTree;
		}
	}

	private static void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs args)
	{
		if (sender is not DataGrid dataGrid)
		{
			return;
		}

		var subscription = dataGrid.GetValue(_subscriptionProperty);
		subscription?.Dispose();
		dataGrid.SetValue(_subscriptionProperty, null);
		dataGrid.DetachedFromVisualTree -= OnDetachedFromVisualTree;
	}

	private static void CloseEditor(DataGrid dataGrid)
	{
		if (Dispatcher.UIThread.CheckAccess())
		{
			dataGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
			return;
		}

		Dispatcher.UIThread.Post(() =>
			dataGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true));
	}
}
