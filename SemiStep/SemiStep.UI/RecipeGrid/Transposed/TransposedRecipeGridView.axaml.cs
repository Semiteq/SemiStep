using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
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
	private readonly TransposedGridNavigator _gridNavigator;
	private readonly TransposedGridSelectionController _gridSelectionController;
	private (RecipeRowViewModel Row, string ColumnKey)? _pendingChangedCell;
	private bool _syncingSelectionFromSurface;

	public TransposedRecipeGridView()
	{
		InitializeComponent();
		_gridNavigator = new TransposedGridNavigator(StepListBox);
		_gridSelectionController = new TransposedGridSelectionController(StepListBox);

		// Cell templates and style resources must be in place before the first layout pass
		// realizes containers, and WhenActivated only fires on Loaded (after that pass) — so
		// this subscription lives on the constructor, keyed off the DataContext-driven
		// ViewModel property.
		this.WhenAnyValue(x => x.ViewModel)
			.Subscribe(surface =>
			{
				InstallCellTemplates(surface);
				if (surface is not null)
				{
					ApplyGridStyle(surface.GridStyle);
				}
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
			_gridSelectionController.HandleCellSelectionPress(ViewModel, pressedCell, e);
		}

		ResolveChangedCellClickAway(pressedCell);
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

	// An always-live editor holding keyboard focus counts as editing (there is no DataGrid edit
	// lifecycle). ComboBox is resolved through the logical tree so an open dropdown (focus inside
	// the popup's own visual root) still counts.
	private Control? GetActiveEditor()
	{
		if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Control focused)
		{
			return null;
		}

		var editor = focused.FindAncestorOfType<TextBox>(includeSelf: true) as Control
			?? focused.FindLogicalAncestorOfType<ComboBox>(includeSelf: true);
		if (editor is null)
		{
			return null;
		}

		return ReferenceEquals(editor.FindLogicalAncestorOfType<TransposedRecipeGridView>(), this)
			? editor
			: null;
	}

	private void InstallCellTemplates(TransposedRecipeGridSurface? surface)
	{
		DataTemplates.Clear();

		if (surface is null)
		{
			return;
		}

		foreach (var template in new TransposedCellTemplateFactory(surface).CreateTemplates())
		{
			DataTemplates.Add(template);
		}
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
