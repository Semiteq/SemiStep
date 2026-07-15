using System.Linq;
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

		// Pool and style resources must exist before the first layout pass; WhenActivated fires only on Loaded, so this lives in the constructor.
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
			// Tunnel: a press inside a cell editor never bubbles to the ListBoxItem, so selection/click-away must hook first.
			StepListBox.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);
			// Tunnel: arrow keys must not reach a closed ComboBox (it cycles its value on arrows).
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

	// Re-applies selection after an orientation flip: the surface got the selection while the view was flipped away.
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
		if (_syncingSelectionFromSurface || ViewModel is null)
		{
			return;
		}

		// SelectedIndexes is a live, ascending view over the selection model's ranges; materialize it
		// before handing it to the surface. It is index-aligned with StepColumns (the same ItemsSource).
		var indices = StepListBox.Selection.SelectedIndexes.ToList();
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

		// Header/marker presses resolve no cell; the pending changed cell stays armed.
		if (TransposedGridCellLocator.ResolveCell(source, StepListBox) is not { } pressedCell)
		{
			return;
		}

		// Selection is left-button only; click-away resolution runs for any button.
		if (e.GetCurrentPoint(StepListBox).Properties.IsLeftButtonPressed)
		{
			if (_gridSelectionController.HandleCellSelectionPress(ViewModel, pressedCell, e))
			{
				TryEnterEditFromPointer(source, pressedCell, e);
			}
		}
		else
		{
			// Consume a right/middle press so native ListBoxItem selection doesn't collapse the multi-selection.
			e.Handled = true;
		}

		ResolveChangedCellClickAway(pressedCell);
	}

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

		// A press inside the open editor must reach the TextBox to reposition the caret; only the entry press is consumed.
		if (presenter.IsEditing)
		{
			return;
		}

		_editCoordinator.BeginEdit(presenter, initialText: null);
		e.Handled = true;
	}

	// No IsReadOnly guard: click-away is not an edit, so it must run during PLC-sync read-only.
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

	// A focused display is NOT editing, so arrow-navigating onto a cell keeps step-level hotkeys live.
	private Control? GetActiveEditor()
	{
		return _editCoordinator.ActiveEditor;
	}

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
