using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

using ReactiveUI;
using ReactiveUI.Avalonia;

using SemiStep.Core.Configuration;

namespace SemiStep.UI.RecipeGrid.Transposed;

public partial class TransposedRecipeGridView : ReactiveUserControl<TransposedRecipeGridSurface>
{
	internal const string CellHeightResourceKey = "TransposedCellHeight";
	internal const string StepColumnWidthResourceKey = "TransposedStepColumnWidth";
	internal const string MarkerSlotHeightResourceKey = "TransposedMarkerSlotHeight";

	private const double CurrentStepMarkerHeight = 4;
	private const double MinimumStepColumnWidth = 96;
	private const double FontSizeToColumnWidthFactor = 8;

	private readonly TransposedStepColumnClassBinder _stepColumnClassBinder = new();
	private readonly TransposedTextEditCoordinator _editCoordinator = new();
	private readonly TransposedGridNavigator _gridNavigator;
	private readonly TransposedGridSelectionController _gridSelectionController;
	private (RecipeRowViewModel Row, string ColumnKey)? _pendingChangedCell;
	private bool _syncingSelectionFromSurface;

	public TransposedRecipeGridView()
	{
		InitializeComponent();
		_gridNavigator = new TransposedGridNavigator(StepListBox);
		_gridSelectionController = new TransposedGridSelectionController(StepListBox);

		// The cell-presenter pool and style resources must be in place before the first layout pass
		// realizes containers (each container's host borrows a presenter from the pool), and
		// WhenActivated only fires on Loaded (after that pass) — so this subscription lives on the
		// constructor, keyed off the DataContext-driven ViewModel property.
		this.WhenAnyValue(x => x.ViewModel)
			.Subscribe(surface =>
			{
				if (surface is not null)
				{
					ApplyGridStyle(surface.GridStyle);
				}

				BuildColumnCellsPool(surface);
			});

		this.WhenActivated(disposables =>
		{
			StepListBox.ContainerPrepared += OnContainerPrepared;
			StepListBox.ContainerClearing += OnContainerClearing;
			StepListBox.SelectionChanged += OnSelectionChanged;
			// Tunnel: a press inside a TextBox/ComboBox cell never bubbles to the ListBoxItem
			// (the editor swallows it), so column selection and changed-cell click-away hook in
			// before the editor sees the press.
			StepListBox.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);
			// Tunnel: arrow keys must not fall through to the always-live editors (a closed
			// ComboBox cycles its value on arrows), so grid navigation intercepts first.
			AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);

			// Activation fires on Loaded, after the first layout pass has already realized
			// containers; stamp those retroactively so their execution classes bind too.
			foreach (var container in StepListBox.GetRealizedContainers())
			{
				_stepColumnClassBinder.OnContainerPrepared(container);
			}

			var selectionRequestsSubscription = new SerialDisposable()
				.DisposeWith(disposables);
			var editorMustCloseSubscription = new SerialDisposable()
				.DisposeWith(disposables);

			this.WhenAnyValue(x => x.ViewModel)
				.Subscribe(surface =>
				{
					if (surface is null)
					{
						selectionRequestsSubscription.Disposable = null;
						editorMustCloseSubscription.Disposable = null;

						return;
					}

					selectionRequestsSubscription.Disposable =
						surface.SelectionRequests.Subscribe(OnSelectionRequested);
					editorMustCloseSubscription.Disposable =
						surface.EditorMustClose.Subscribe(_ => CloseActiveEditor());
				})
				.DisposeWith(disposables);

			Disposable.Create(() =>
			{
				StepListBox.ContainerPrepared -= OnContainerPrepared;
				StepListBox.ContainerClearing -= OnContainerClearing;
				StepListBox.SelectionChanged -= OnSelectionChanged;
				StepListBox.RemoveHandler(PointerPressedEvent, OnGridPointerPressed);
				RemoveHandler(KeyDownEvent, OnTunnelKeyDown);
				_pendingChangedCell = null;
				_stepColumnClassBinder.Reset();
			}).DisposeWith(disposables);
		});
	}

	public bool IsEditing => GetActiveEditor() is not null;

	internal TransposedColumnCellsPool? ColumnCellsPool { get; private set; }

	private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
	{
		_stepColumnClassBinder.OnContainerPrepared(e.Container);
	}

	private void OnContainerClearing(object? sender, ContainerClearingEventArgs e)
	{
		_stepColumnClassBinder.OnContainerClearing(e.Container);
	}

	// Re-applies the surface's selection to the ListBox. The host calls this when the view
	// becomes active after an orientation flip: the surface received the carried-over selection
	// while the view was flipped away, so the visual selection is stale by then.
	internal void SyncSelectionFromSurface()
	{
		if (ViewModel is null || StepListBox.SelectedItems is not { } selectedItems)
		{
			return;
		}

		_syncingSelectionFromSurface = true;
		try
		{
			selectedItems.Clear();
			foreach (var index in ViewModel.SelectedStepIndices)
			{
				if (index >= 0 && index < ViewModel.StepColumns.Count)
				{
					selectedItems.Add(ViewModel.StepColumns[index]);
				}
			}
		}
		finally
		{
			_syncingSelectionFromSurface = false;
		}

		if (StepListBox.SelectedIndex >= 0)
		{
			StepListBox.ScrollIntoView(StepListBox.SelectedIndex);
		}
	}

	private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_syncingSelectionFromSurface
			|| ViewModel is null
			|| StepListBox.SelectedItems is not { } selectedItems)
		{
			return;
		}

		var indices = new List<int>();
		foreach (var item in selectedItems)
		{
			if (item is StepColumnViewModel stepColumn)
			{
				var index = ViewModel.StepColumns.IndexOf(stepColumn);
				if (index >= 0)
				{
					indices.Add(index);
				}
			}
		}

		indices.Sort();
		ViewModel.UpdateSelection(indices);
	}

	private void OnSelectionRequested(int? suggestedIndex)
	{
		if (ViewModel is null)
		{
			return;
		}

		if (suggestedIndex is null)
		{
			StepListBox.SelectedIndex = -1;
			return;
		}

		var index = suggestedIndex.Value;
		if (index < 0 || index >= ViewModel.StepColumns.Count)
		{
			return;
		}

		StepListBox.SelectedIndex = index;
		StepListBox.ScrollIntoView(index);
	}

	private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
	{
		if (e.Source is not Visual source)
		{
			return;
		}

		// Header/marker presses resolve no cell: native ListBoxItem selection handles them and
		// the pending changed cell stays armed (canonical parity: only cell presses resolve).
		if (TransposedGridCellLocator.ResolveCell(source, StepListBox) is not { } pressedCell)
		{
			return;
		}

		// Selection is a left-button gesture (canonical parity: right/middle clicks never
		// collapse a multi-selection); click-away resolution runs for any button, matching
		// canonical's CellPointerPressed.
		if (e.GetCurrentPoint(StepListBox).Properties.IsLeftButtonPressed)
		{
			if (_gridSelectionController.HandleCellSelectionPress(ViewModel, pressedCell, e))
			{
				TryEnterEditFromPointer(source, pressedCell, e);
			}
		}
		else
		{
			// A right/middle press over a cell must not reach the ListBoxItem's native selection (which
			// would collapse a multi-selection). The lazy display TextBlock does not swallow it the way the
			// always-live editor once did, so consume it here. The context menu opens off ContextRequested,
			// which is independent of the handled state of this press.
			e.Handled = true;
		}

		ResolveChangedCellClickAway(pressedCell);
	}

	// Select-then-edit under the lazy display: the selection controller reports a fall-through only on a
	// second press of the already-single-selected column. That press builds and focuses the cell's editor
	// (a TextBox for property-text, a ComboBox with its dropdown opened for a combo). A read-only /
	// inapplicable cell's display is non-hit-testable, so the press never resolves to its presenter and
	// falls through to plain column selection.
	private void TryEnterEditFromPointer(
		Visual source,
		ParameterCellViewModel pressedCell,
		PointerPressedEventArgs e)
	{
		if (source.FindAncestorOfType<TransposedLazyCellPresenter>(includeSelf: true) is not { } presenter
			|| !ReferenceEquals(presenter.DataContext, pressedCell))
		{
			return;
		}

		// A press inside the already-open editor must reach the live TextBox to reposition the caret, so
		// leave it unhandled: only the entry press (a display not yet editing) is consumed. This is the
		// tunnel phase, so a handled press never reaches the editor.
		if (presenter.IsEditing)
		{
			return;
		}

		_editCoordinator.BeginEdit(presenter, initialText: null);
		e.Handled = true;
	}

	// Click-away rule: a changed (orange) cell clears the moment any OTHER cell is pressed. This
	// is not an edit, so it must run even while the surface is read-only during PLC sync — no
	// IsReadOnly guard (mirror of CanonicalRecipeGridView.OnCellPointerPressed). The clear routes
	// through the surface so the sibling orientation surface clears too.
	private void ResolveChangedCellClickAway(ParameterCellViewModel pressedCell)
	{
		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve(
			_pendingChangedCell,
			pressedCell.Row,
			pressedCell.Descriptor.ParameterKey);

		if (cellToClear is { } toClear)
		{
			ViewModel?.ClearChangedByClickAway(toClear.Row, toClear.ColumnKey);
		}

		_pendingChangedCell = newPending;
	}

	private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
	{
		_gridNavigator.HandleTunnelKeyDown(ViewModel, e);
	}

	private void CloseActiveEditor()
	{
		if (GetActiveEditor() is not { } editor)
		{
			return;
		}

		if (editor is ComboBox comboBox)
		{
			comboBox.IsDropDownOpen = false;
		}

		TransposedCellTemplateFactory.CommitByDefocusing(editor);
	}

	// Editing is defined off the coordinator, which owns the one active lazy edit for both cell kinds (the
	// heavy editor exists only while editing). A focused display visual is NOT editing, so arrow-navigating
	// onto a cell leaves IsEditing false and the step-level hotkeys stay live. A combo whose dropdown is
	// open still counts (the coordinator holds it as editing until the dropdown closes and focus leaves).
	private Control? GetActiveEditor()
	{
		return _editCoordinator.ActiveEditor;
	}

	// A fresh pool per surface: the singleton view is rebound to a new surface on a config swap, whose
	// descriptor set (and thus the built cell subtrees) differ, so presenters must not carry across.
	// Hosts return their borrowed presenter to the pool that lent it, so the stale pool drains to GC.
	private void BuildColumnCellsPool(TransposedRecipeGridSurface? surface)
	{
		// The prior surface's pooled presenters (and their editors) are discarded with the pool, so the
		// coordinator must forget any edit it was tracking before the new pool's slots come alive.
		_editCoordinator.Reset();

		ColumnCellsPool = surface is null
			? null
			: new TransposedColumnCellsPool(
				surface.ParameterDescriptors,
				new TransposedCellTemplateFactory(surface, _editCoordinator),
				surface.GridStyle.RowHeight);
	}

	// The step-number header cell is the transposed analog of canonical's step-number cells (cell
	// font, per ColumnBuilder.AddNumberingColumn); the parameter-name column is the analog of
	// canonical's column headers (header font). Both inherit through TextElement attached values.
	private void ApplyGridStyle(GridStyleOptions gridStyle)
	{
		Resources[CellHeightResourceKey] = gridStyle.RowHeight;
		Resources[MarkerSlotHeightResourceKey] = CurrentStepMarkerHeight;
		Resources[StepColumnWidthResourceKey] =
			Math.Max(MinimumStepColumnWidth, gridStyle.CellFontSize * FontSizeToColumnWidthFactor);

		GridFontApplier.ApplyCellFont(StepListBox, gridStyle);
		GridFontApplier.ApplyHeaderFont(ParameterNameColumn, gridStyle);
	}
}
