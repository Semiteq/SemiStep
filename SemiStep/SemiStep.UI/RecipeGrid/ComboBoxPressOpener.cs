using System.Reactive.Disposables;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid;

/// <summary>Starts a canonical combo cell's edit, and so opens its list, on a plain left press.</summary>
internal sealed class ComboBoxPressOpener(DataGrid dataGrid, CellEditCoordinator editCoordinator)
{
	private LazyComboCellPresenter? _presenterToOpen;

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

	// Decided before the press closes a live list: a press that ends one edit never starts another.
	private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		var isPlainLeftPress = e.KeyModifiers == KeyModifiers.None
			&& e.GetCurrentPoint(dataGrid).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed;
		_presenterToOpen = isPlainLeftPress
			&& editCoordinator.ActiveEditor is null
			&& PresenterOf(e.Source) is { IsEditing: false, CanEnterEdit: true } presenter
				? presenter
				: null;
	}

	// Entered after the DataGrid has taken the press for the row selection, so the opening list keeps the keyboard.
	private void OnBubblePointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (_presenterToOpen is { } presenter)
		{
			_presenterToOpen = null;
			editCoordinator.BeginEdit(presenter, initialText: null);
		}
	}

	private LazyComboCellPresenter? PresenterOf(object? source)
	{
		return source is Visual visual
			&& visual.FindAncestorOfType<LazyComboCellPresenter>(includeSelf: true) is { } presenter
			&& dataGrid.IsVisualAncestorOf(presenter)
				? presenter
				: null;
	}
}
