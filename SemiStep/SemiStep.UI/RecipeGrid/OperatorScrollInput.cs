using System.Reactive.Disposables;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Reports operator scroll input on a grid: a navigation key, the mouse wheel or a scroll bar drag or click, whether
/// or not the view moved. Code-driven scrolling raises none of these, so it never counts.
/// </summary>
internal static class OperatorScrollInput
{
	private static readonly HashSet<Key> _verticalKeys = [Key.Up, Key.Down, Key.PageUp, Key.PageDown, Key.Home, Key.End];

	/// <summary>Watches <paramref name="grid"/> and every scroll bar inside it, also one created later.</summary>
	public static IDisposable Watch(Control grid, Action onOperatorScroll, bool stepsRunHorizontally)
	{
		// Tunnel with handled events: the grid's own key handlers must not hide a key from this one.
		void OnKeyDown(object? sender, KeyEventArgs e)
		{
			var isStepKey = stepsRunHorizontally && e.Key is Key.Left or Key.Right;
			if ((_verticalKeys.Contains(e.Key) || isStepKey) && !IsInsideEditor(e.Source))
			{
				onOperatorScroll();
			}
		}

		void OnWheel(object? sender, PointerWheelEventArgs e)
		{
			onOperatorScroll();
		}

		// ScrollBar.Scroll is a CLR event, so the grid listens to the routed thumb drag and track clicks instead.
		void OnScrollBarPart(object? sender, RoutedEventArgs e)
		{
			if (e.Source is Visual source && source.FindAncestorOfType<ScrollBar>() is not null)
			{
				onOperatorScroll();
			}
		}

		grid.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
		grid.AddHandler(
			InputElement.PointerWheelChangedEvent,
			OnWheel,
			RoutingStrategies.Tunnel,
			handledEventsToo: true);
		grid.AddHandler(Thumb.DragDeltaEvent, OnScrollBarPart, handledEventsToo: true);
		grid.AddHandler(Button.ClickEvent, OnScrollBarPart, handledEventsToo: true);

		return Disposable.Create(() =>
		{
			grid.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
			grid.RemoveHandler(InputElement.PointerWheelChangedEvent, OnWheel);
			grid.RemoveHandler(Thumb.DragDeltaEvent, OnScrollBarPart);
			grid.RemoveHandler(Button.ClickEvent, OnScrollBarPart);
		});
	}

	// Home, End and the arrows move the caret or the list highlight of an open cell editor, not the view.
	private static bool IsInsideEditor(object? source)
	{
		return source is Visual visual
			&& (visual.FindAncestorOfType<TextBox>(includeSelf: true) is not null
				|| visual.FindAncestorOfType<ComboBox>(includeSelf: true) is not null);
	}
}
