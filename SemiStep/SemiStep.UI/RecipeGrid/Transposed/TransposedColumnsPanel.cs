using System;
using System.Collections.Generic;
using System.Collections.Specialized;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace SemiStep.UI.RecipeGrid.Transposed;

/// <summary>
/// A horizontal virtualizing panel for the transposed step-column grid that recycles containers
/// in place. Idle containers are hidden (<see cref="Visual.IsVisibleProperty"/> = false) and pushed
/// to an idle stack instead of being detached, so a container is added to the visual tree exactly
/// once and reused for its whole life. This removes the per-recycle style-attach, property-store
/// growth and composition-visual creation that the framework <c>VirtualizingStackPanel</c> pays on
/// every viewport crossing.
///
/// Uniform column width is load-bearing: it makes the viewport-to-index math exact, so there is no
/// size estimation or scroll anchoring.
/// </summary>
public sealed class TransposedColumnsPanel : VirtualizingPanel
{
	// Off-screen columns realized on each side of the viewport, so a scroll does not flicker an
	// empty edge before the newly exposed column is realized.
	private const int BufferColumns = 2;

	// Sub-pixel guard: a viewport right edge landing exactly on a column boundary must not count the
	// next column as visible and realize one extra column.
	private const double ViewportEdgeEpsilon = 0.5;

	/// <summary>
	/// The uniform width of every step column. Bound in the <c>ItemsPanelTemplate</c> to the
	/// <c>TransposedStepColumnWidth</c> resource.
	/// </summary>
	public static readonly StyledProperty<double> ColumnWidthProperty =
		AvaloniaProperty.Register<TransposedColumnsPanel, double>(nameof(ColumnWidth), defaultValue: 96d);

	private readonly Dictionary<int, Control> _realized = new();
	private readonly Stack<Control> _idle = new();
	private readonly List<int> _indicesToUnrealize = new();
	private readonly List<KeyValuePair<int, Control>> _shiftBuffer = new();

	private Rect _viewport;
	private double _maxRealizedChildHeight;

	// Set while a measure/arrange pass runs so a re-entrant ScrollIntoView (which itself drives a layout
	// pass) bails instead of recursing into realization from inside layout.
	private bool _isInLayout;

	// The selection-anchor container is deferred rather than unrealized when it scrolls out of the
	// window, so an editor or focus it holds survives offscreen. It is released once the anchor moves.
	private Control? _deferredElement;
	private int _deferredIndex = -1;

	static TransposedColumnsPanel()
	{
		AffectsMeasure<TransposedColumnsPanel>(ColumnWidthProperty);
	}

	public TransposedColumnsPanel()
	{
		EffectiveViewportChanged += OnEffectiveViewportChanged;
	}

	public double ColumnWidth
	{
		get => GetValue(ColumnWidthProperty);
		set => SetValue(ColumnWidthProperty, value);
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		_isInLayout = true;
		try
		{
			var items = Items;
			var count = items.Count;

			if (count == 0)
			{
				UnrealizeAll();

				return default;
			}

			var (firstIndex, lastIndex) = CalculateRealizedRange(count);

			UnrealizeOutsideRange(firstIndex, lastIndex);

			_maxRealizedChildHeight = 0;
			for (var index = firstIndex; index <= lastIndex; index++)
			{
				var container = Realize(index, items);
				container.Measure(availableSize);
				_maxRealizedChildHeight = Math.Max(_maxRealizedChildHeight, container.DesiredSize.Height);
			}

			// Keep the deferred anchor laid out while it lives offscreen.
			if (_deferredElement is { } deferred)
			{
				deferred.Measure(availableSize);
				_maxRealizedChildHeight = Math.Max(_maxRealizedChildHeight, deferred.DesiredSize.Height);
			}

			return new Size(count * ColumnWidth, _maxRealizedChildHeight);
		}
		finally
		{
			_isInLayout = false;
		}
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		_isInLayout = true;
		try
		{
			var columnWidth = ColumnWidth;

			foreach (var (index, container) in _realized)
			{
				container.Arrange(new Rect(index * columnWidth, 0, columnWidth, finalSize.Height));
			}

			if (_deferredElement is { } deferred && _deferredIndex >= 0)
			{
				deferred.Arrange(new Rect(_deferredIndex * columnWidth, 0, columnWidth, finalSize.Height));
			}

			return finalSize;
		}
		finally
		{
			_isInLayout = false;
		}
	}

	protected override void OnItemsControlChanged(ItemsControl? oldValue)
	{
		base.OnItemsControlChanged(oldValue);

		if (oldValue is not null)
		{
			oldValue.PropertyChanged -= OnItemsControlPropertyChanged;
		}

		if (ItemsControl is not null)
		{
			ItemsControl.PropertyChanged += OnItemsControlPropertyChanged;
		}
	}

	protected override void OnItemsChanged(IReadOnlyList<object?> items, NotifyCollectionChangedEventArgs e)
	{
		InvalidateMeasure();

		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				OnItemsInserted(e.NewStartingIndex, e.NewItems!.Count);
				break;

			case NotifyCollectionChangedAction.Remove:
				OnItemsRemoved(e.OldStartingIndex, e.OldItems!.Count);
				break;

			case NotifyCollectionChangedAction.Replace:
				// Replace keeps indices stable; drop the affected containers so the next measure pass
				// rebinds fresh containers to the replacement items.
				UnrealizeRange(e.OldStartingIndex, e.OldItems!.Count);
				break;

			case NotifyCollectionChangedAction.Move:
				OnItemsMoved(e);
				break;

			case NotifyCollectionChangedAction.Reset:
				OnItemsReset();
				break;
		}
	}

	protected override Control? ScrollIntoView(int index)
	{
		var items = Items;

		if (_isInLayout || index < 0 || index >= items.Count || !IsEffectivelyVisible)
		{
			return null;
		}

		if (ContainerFromIndex(index) is { } realized)
		{
			realized.BringIntoView();

			return realized;
		}

		// The target is outside the realized window. Realize it eagerly and place it at its exact rect so
		// BringIntoView has real bounds to reveal, then settle with a single layout pass. The uniform-width
		// extent is already exact from the first measure, so no multi-pass extent compensation is needed.
		var container = Realize(index, items);
		container.Measure(Size.Infinity);

		var columnWidth = ColumnWidth;
		var height = _maxRealizedChildHeight > 0 ? _maxRealizedChildHeight : container.DesiredSize.Height;
		container.Arrange(new Rect(index * columnWidth, 0, columnWidth, height));

		container.BringIntoView();
		UpdateLayout();

		// The settling pass may have re-realized the target into the window; return whatever now sits at
		// the index. If the viewport was already at a scroll limit and could not move, that pass may have
		// recycled the eager container to idle (hidden) — re-realize it so navigation never lands on a
		// hidden container.
		return ContainerFromIndex(index) ?? Realize(index, items);
	}

	protected override Control? ContainerFromIndex(int index)
	{
		if (index < 0 || index >= Items.Count)
		{
			return null;
		}

		if (_deferredIndex == index)
		{
			return _deferredElement;
		}

		return _realized.GetValueOrDefault(index);
	}

	protected override int IndexFromContainer(Control container)
	{
		if (ReferenceEquals(container, _deferredElement))
		{
			return _deferredIndex;
		}

		foreach (var (index, realized) in _realized)
		{
			if (ReferenceEquals(realized, container))
			{
				return index;
			}
		}

		return -1;
	}

	protected override IEnumerable<Control>? GetRealizedContainers()
	{
		return _realized.Values;
	}

	protected override IInputElement? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
	{
		var count = Items.Count;
		var fromControl = from as Control;

		if (count == 0
			|| (fromControl is null && direction is not NavigationDirection.First and not NavigationDirection.Last))
		{
			return null;
		}

		var fromIndex = fromControl is not null ? IndexFromContainer(fromControl) : -1;
		var toIndex = fromIndex;

		switch (direction)
		{
			case NavigationDirection.First:
				toIndex = 0;
				break;
			case NavigationDirection.Last:
				toIndex = count - 1;
				break;
			case NavigationDirection.Next:
			case NavigationDirection.Right:
				toIndex++;
				break;
			case NavigationDirection.Previous:
			case NavigationDirection.Left:
				toIndex--;
				break;
			case NavigationDirection.Up:
			case NavigationDirection.Down:
				// A single horizontal row has no vertical neighbour; keep focus where it is.
				break;
			default:
				return null;
		}

		if (fromIndex == toIndex)
		{
			return from;
		}

		if (wrap)
		{
			if (toIndex < 0)
			{
				toIndex = count - 1;
			}
			else if (toIndex >= count)
			{
				toIndex = 0;
			}
		}

		// Resolve past the realization boundary: ScrollIntoView realizes the target so ListBox keyboard
		// range-extend (Shift+Arrow), Home/End and Page navigation do not dead-end at an idle column.
		return ScrollIntoView(toIndex);
	}

	private (int FirstIndex, int LastIndex) CalculateRealizedRange(int count)
	{
		var columnWidth = ColumnWidth;

		if (columnWidth <= 0 || _viewport.Width <= 0)
		{
			// The viewport is not known yet (before the first EffectiveViewportChanged); realize a
			// small window from the start so the panel has content and a measured height.
			return (0, Math.Min(count - 1, BufferColumns));
		}

		var firstVisible = (int)Math.Floor(_viewport.Left / columnWidth);
		var lastVisible = (int)Math.Floor((_viewport.Right - ViewportEdgeEpsilon) / columnWidth);

		var firstIndex = Math.Clamp(firstVisible - BufferColumns, 0, count - 1);
		var lastIndex = Math.Clamp(lastVisible + BufferColumns, 0, count - 1);

		return (firstIndex, lastIndex);
	}

	private void UnrealizeOutsideRange(int firstIndex, int lastIndex)
	{
		_indicesToUnrealize.Clear();
		foreach (var index in _realized.Keys)
		{
			if (index < firstIndex || index > lastIndex)
			{
				_indicesToUnrealize.Add(index);
			}
		}

		foreach (var index in _indicesToUnrealize)
		{
			Unrealize(index);
		}
	}

	private void UnrealizeAll()
	{
		_indicesToUnrealize.Clear();
		_indicesToUnrealize.AddRange(_realized.Keys);
		foreach (var index in _indicesToUnrealize)
		{
			Unrealize(index);
		}
	}

	private Control Realize(int index, IReadOnlyList<object?> items)
	{
		if (_realized.TryGetValue(index, out var existing))
		{
			return existing;
		}

		// The deferred anchor is already prepared and visible; reclaim it without re-preparing.
		if (_deferredIndex == index && _deferredElement is { } deferred)
		{
			_deferredElement = null;
			_deferredIndex = -1;
			_realized[index] = deferred;

			return deferred;
		}

		var item = items[index];
		var generator = ItemContainerGenerator!;
		Control container;

		if (_idle.Count > 0)
		{
			// Keep-attached reuse: the container is already a child, so no AddInternalChild.
			container = _idle.Pop();
			container.SetCurrentValue(Visual.IsVisibleProperty, true);
			generator.PrepareItemContainer(container, item, index);
			generator.ItemContainerPrepared(container, item, index);
		}
		else
		{
			// First realize of a physical container: the generator contract order, AddInternalChild once.
			generator.NeedsContainer(item, index, out var recycleKey);
			container = generator.CreateContainer(item, index, recycleKey);
			generator.PrepareItemContainer(container, item, index);
			AddInternalChild(container);
			generator.ItemContainerPrepared(container, item, index);
		}

		_realized[index] = container;

		return container;
	}

	private void Unrealize(int index)
	{
		if (!_realized.TryGetValue(index, out var container))
		{
			return;
		}

		_realized.Remove(index);

		// Defer the selection-anchor container instead of unrealizing it: an open editor or focus it
		// holds must survive scrolling offscreen. It is released via the TabOnceActiveElement listener.
		if (ItemsControl is { } itemsControl
			&& ReferenceEquals(KeyboardNavigation.GetTabOnceActiveElement(itemsControl), container))
		{
			_deferredElement = container;
			_deferredIndex = index;

			return;
		}

		RecycleToIdle(container);
	}

	private void RecycleToIdle(Control container)
	{
		ItemContainerGenerator!.ClearItemContainer(container);
		container.SetCurrentValue(Visual.IsVisibleProperty, false);
		_idle.Push(container);
	}

	private void OnItemsInserted(int index, int count)
	{
		if (count <= 0)
		{
			return;
		}

		// Every realized container at or after the insertion point keeps its data item but gains
		// a higher index; shift its key up and notify the generator of the new index.
		ShiftRealizedKeys(fromIndex: index, delta: count);

		if (_deferredElement is { } deferred && _deferredIndex >= index)
		{
			var oldIndex = _deferredIndex;
			_deferredIndex += count;
			ItemContainerGenerator?.ItemContainerIndexChanged(deferred, oldIndex, _deferredIndex);
		}
	}

	private void OnItemsRemoved(int index, int count)
	{
		if (count <= 0)
		{
			return;
		}

		// Unrealize the removed items' containers first so none stays mapped to a gone item, then
		// shift the survivors after the gap down into their new indices.
		UnrealizeRange(index, count);
		ShiftRealizedKeys(fromIndex: index + count, delta: -count);

		if (_deferredElement is { } deferred && _deferredIndex >= index + count)
		{
			var oldIndex = _deferredIndex;
			_deferredIndex -= count;
			ItemContainerGenerator?.ItemContainerIndexChanged(deferred, oldIndex, _deferredIndex);
		}
	}

	private void OnItemsMoved(NotifyCollectionChangedEventArgs e)
	{
		// A move with no source index is a bulk reorder the panel cannot map incrementally; treat it
		// as a reset (mirrors VirtualizingStackPanel).
		if (e.OldStartingIndex < 0)
		{
			OnItemsReset();

			return;
		}

		OnItemsRemoved(e.OldStartingIndex, e.OldItems!.Count);

		var insertIndex = e.NewStartingIndex;
		if (e.NewStartingIndex > e.OldStartingIndex)
		{
			insertIndex -= e.OldItems!.Count - 1;
		}

		OnItemsInserted(insertIndex, e.NewItems!.Count);
	}

	private void UnrealizeRange(int index, int count)
	{
		_indicesToUnrealize.Clear();
		foreach (var realizedIndex in _realized.Keys)
		{
			if (realizedIndex >= index && realizedIndex < index + count)
			{
				_indicesToUnrealize.Add(realizedIndex);
			}
		}

		foreach (var realizedIndex in _indicesToUnrealize)
		{
			var container = _realized[realizedIndex];
			_realized.Remove(realizedIndex);
			RecycleToIdle(container);
		}

		// A removed or replaced item's deferred anchor no longer maps to any item, so release it too.
		if (_deferredElement is { } deferred && _deferredIndex >= index && _deferredIndex < index + count)
		{
			_deferredElement = null;
			_deferredIndex = -1;
			RecycleToIdle(deferred);
		}
	}

	private void ShiftRealizedKeys(int fromIndex, int delta)
	{
		if (delta == 0 || _realized.Count == 0)
		{
			return;
		}

		_shiftBuffer.Clear();
		foreach (var entry in _realized)
		{
			if (entry.Key >= fromIndex)
			{
				_shiftBuffer.Add(entry);
			}
		}

		if (_shiftBuffer.Count == 0)
		{
			return;
		}

		// Drop the old keys before re-adding the shifted ones so an up-shift never collides with a key
		// it is about to overwrite.
		foreach (var entry in _shiftBuffer)
		{
			_realized.Remove(entry.Key);
		}

		var generator = ItemContainerGenerator;
		foreach (var entry in _shiftBuffer)
		{
			var newIndex = entry.Key + delta;
			_realized[newIndex] = entry.Value;
			generator?.ItemContainerIndexChanged(entry.Value, entry.Key, newIndex);
		}
	}

	private void OnItemsReset()
	{
		var generator = ItemContainerGenerator;

		// Clear the still-mapped containers (realized + deferred). Idle containers were already cleared
		// when they were unrealized, so clearing them again would double-fire ContainerClearing.
		if (generator is not null)
		{
			foreach (var container in _realized.Values)
			{
				generator.ClearItemContainer(container);
			}

			if (_deferredElement is { } deferred)
			{
				generator.ClearItemContainer(deferred);
			}
		}

		_realized.Clear();
		_deferredElement = null;
		_deferredIndex = -1;
		_maxRealizedChildHeight = 0;

		// Physically detach every child (realized + idle) so each host leaves the visual tree and
		// releases its pooled presenter — the surface-swap teardown the pool lifecycle depends on.
		while (Children.Count > 0)
		{
			RemoveInternalChild(Children[Children.Count - 1]);
		}

		_idle.Clear();
	}

	private void OnItemsControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (_deferredElement is null
			|| e.Property != KeyboardNavigation.TabOnceActiveElementProperty
			|| !ReferenceEquals(e.GetOldValue<IInputElement?>(), _deferredElement))
		{
			return;
		}

		// The anchor moved off the deferred container, so it can be unrealized for real now.
		var container = _deferredElement;
		_deferredElement = null;
		_deferredIndex = -1;

		RecycleToIdle(container);
		InvalidateMeasure();
	}

	private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
	{
		var newViewport = e.EffectiveViewport.Intersect(new Rect(Bounds.Size));

		if (newViewport == _viewport)
		{
			return;
		}

		_viewport = newViewport;
		InvalidateMeasure();
	}
}
