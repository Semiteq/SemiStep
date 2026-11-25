using System.Reactive.Disposables;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid;

/// <summary>Keeps the mouse wheel and typing on a closed combo cell from changing its value.</summary>
internal sealed class ClosedComboInputGuard(DataGrid dataGrid)
{
	public IDisposable Attach()
	{
		dataGrid.AddHandler(
			InputElement.PointerWheelChangedEvent,
			OnTunnelPointerWheelChanged,
			RoutingStrategies.Tunnel);
		dataGrid.AddHandler(InputElement.TextInputEvent, OnTunnelTextInput, RoutingStrategies.Tunnel);

		return Disposable.Create(() =>
		{
			dataGrid.RemoveHandler(InputElement.PointerWheelChangedEvent, OnTunnelPointerWheelChanged);
			dataGrid.RemoveHandler(InputElement.TextInputEvent, OnTunnelTextInput);
		});
	}

	// A closed ComboBox steps its selection on a wheel notch only while it holds focus. Moving focus to the grid
	// before the notch reaches the combo leaves the notch to the DataGrid, which scrolls.
	private void OnTunnelPointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		if (ClosedComboBoxOf(e.Source) is { IsFocused: true })
		{
			dataGrid.Focus();
		}
	}

	// The ComboBox text search selects a matching item whether or not its list is open.
	private static void OnTunnelTextInput(object? sender, TextInputEventArgs e)
	{
		if (ClosedComboBoxOf(e.Source) is not null)
		{
			e.Handled = true;
		}
	}

	private static ComboBox? ClosedComboBoxOf(object? source)
	{
		return source is Visual visual
			&& visual.FindAncestorOfType<ComboBox>(includeSelf: true) is { IsDropDownOpen: false } comboBox
				? comboBox
				: null;
	}
}
