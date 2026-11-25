using System.Reactive.Disposables;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid;

/// <summary>Opens a canonical combo cell's list on a plain left press instead of on the release.</summary>
internal sealed class ComboBoxPressOpener(DataGrid dataGrid)
{
	private const string PressedPseudoClass = ":pressed";

	private ComboBox? _comboBoxToOpen;

	public IDisposable Attach()
	{
		dataGrid.AddHandler(InputElement.PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel);
		dataGrid.AddHandler(
			InputElement.PointerPressedEvent,
			OnBubblePointerPressed,
			RoutingStrategies.Bubble,
			handledEventsToo: true);

		return Disposable.Create(() =>
		{
			dataGrid.RemoveHandler(InputElement.PointerPressedEvent, OnTunnelPointerPressed);
			dataGrid.RemoveHandler(InputElement.PointerPressedEvent, OnBubblePointerPressed);
		});
	}

	// Decided before the ComboBox's own press logic runs, because that logic closes an open list.
	private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		var isPlainLeftPress = e.KeyModifiers == KeyModifiers.None
			&& e.GetCurrentPoint(dataGrid).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed;
		_comboBoxToOpen = isPlainLeftPress && ComboBoxBodyOf(e.Source) is { IsDropDownOpen: false } comboBox
			? comboBox
			: null;
	}

	// Opened after the DataGrid has taken focus for the row selection, so the opening list keeps the keyboard.
	// The ComboBox toggles its list on a release only while it holds the pressed state its own press set; clearing
	// that state here lets the release end every press on a combo body without a toggle.
	private void OnBubblePointerPressed(object? sender, PointerPressedEventArgs e)
	{
		_comboBoxToOpen?.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true);
		_comboBoxToOpen = null;

		if (ComboBoxBodyOf(e.Source) is { } pressedComboBox)
		{
			((IPseudoClasses)pressedComboBox.Classes).Remove(PressedPseudoClass);
		}
	}

	private ComboBox? ComboBoxBodyOf(object? source)
	{
		return source is Visual visual
			&& visual.FindAncestorOfType<ComboBox>(includeSelf: true) is { } comboBox
			&& dataGrid.IsVisualAncestorOf(comboBox)
				? comboBox
				: null;
	}
}
