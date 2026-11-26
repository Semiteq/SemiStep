using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace SemiStep.UI.RecipeGrid;

public partial class CanonicalRecipeGridView : ReactiveUserControl<CanonicalRecipeGridSurface>
{
	private const string CurrentCellPseudoClass = ":current";

	private readonly CellEditCoordinator _comboEditCoordinator = new();
	private bool _columnsBuilt;
	private bool _syncingSelectionFromSurface;
	private (RecipeRowViewModel Row, string ColumnKey)? _pendingChangedCell;

	public CanonicalRecipeGridView()
	{
		InitializeComponent();

		this.WhenActivated(disposables =>
		{
			RecipeGrid.BeginningEdit += OnBeginningEdit;
			RecipeGrid.CellEditEnded += OnCellEditEnded;
			RecipeGrid.SelectionChanged += OnSelectionChanged;
			RecipeGrid.CellPointerPressed += OnCellPointerPressed;
			RecipeGrid.AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel);
			new ComboBoxPressOpener(RecipeGrid, _comboEditCoordinator).Attach().DisposeWith(disposables);
			OperatorScrollInput.Watch(RecipeGrid, () => ViewModel?.NotifyOperatorScroll(), stepsRunHorizontally: false)
				.DisposeWith(disposables);

			var selectionRequestsSubscription = new SerialDisposable()
				.DisposeWith(disposables);
			var runningStepSubscription = new SerialDisposable()
				.DisposeWith(disposables);

			this.WhenAnyValue(x => x.ViewModel)
				.Subscribe(surface =>
				{
					if (surface is null)
					{
						selectionRequestsSubscription.Disposable = null;
						runningStepSubscription.Disposable = null;

						return;
					}

					BuildGrid();
					selectionRequestsSubscription.Disposable =
						surface.SelectionRequests.Subscribe(OnSelectionRequested);
					// Background: the request that greets a view activated mid-run lands before the rows' layout.
					runningStepSubscription.Disposable = surface.RunningStepScrollRequests.Subscribe(stepIndex =>
						Dispatcher.UIThread.Post(() => ScrollStepIntoView(stepIndex), DispatcherPriority.Background));
				})
				.DisposeWith(disposables);

			Disposable.Create(() =>
			{
				RecipeGrid.BeginningEdit -= OnBeginningEdit;
				RecipeGrid.CellEditEnded -= OnCellEditEnded;
				RecipeGrid.SelectionChanged -= OnSelectionChanged;
				RecipeGrid.CellPointerPressed -= OnCellPointerPressed;
				RecipeGrid.RemoveHandler(KeyDownEvent, OnTunnelKeyDown);
				_pendingChangedCell = null;
				IsTextEditing = false;
			}).DisposeWith(disposables);
		});
	}

	public bool IsEditing => IsTextEditing || _comboEditCoordinator.ActiveEditor is not null;

	private bool IsTextEditing { get; set; }

	// Display cells take no focus, so the grid routes the open keys to its current cell.
	private void OnTunnelKeyDown(object? sender, KeyEventArgs e)
	{
		if (IsTextEditing && e.Key == Key.Escape)
		{
			DetachTextEditor(e.Source);
			return;
		}

		if (IsEditing)
		{
			return;
		}

		// Keys and column first: the current-cell lookup walks the realized cells, and arrow auto-repeat or typing
		// into a read-only column must not pay for it.
		if (CellEditKeys.IsEditKey(e) || CellEditKeys.IsListKey(e))
		{
			if (CurrentCell() is { Content: LazyComboCellPresenter { CanEnterEdit: true } presenter })
			{
				_comboEditCoordinator.BeginEdit(presenter, initialText: null);
				e.Handled = true;
			}

			return;
		}

		if (CellEditKeys.IsPrintable(e.KeySymbol, e.KeyModifiers)
			&& CanTypeIntoCurrentColumn()
			&& CurrentCell() is { } cell)
		{
			BeginTypedEdit(cell);
		}
	}

	private bool CanTypeIntoCurrentColumn()
	{
		return !RecipeGrid.IsReadOnly && RecipeGrid.CurrentColumn is { IsReadOnly: false };
	}

	// The KeyDown stays unhandled: Avalonia.Win32 drops the WM_CHAR of a handled KeyDown, and the character it
	// carries, in the keyboard layout and case the user typed, lands on the editor focused here.
	private void BeginTypedEdit(DataGridCell cell)
	{
		if (RecipeGrid.BeginEdit() && cell.Content is TextBox textBox)
		{
			textBox.Text = string.Empty;
			textBox.Focus();
		}
	}

	// The editor writes on LostFocus, and the DataGrid moves focus to itself before it cancels an edit. Detaching
	// the editor's binding first leaves nothing to write, so Esc keeps the stored value as it was.
	private static void DetachTextEditor(object? source)
	{
		if (source is TextBox textBox)
		{
			BindingOperations.GetBindingExpressionBase(textBox, TextBox.TextProperty)?.Dispose();
		}
	}

	private DataGridCell? CurrentCell()
	{
		return RecipeGrid.GetVisualDescendants()
			.OfType<DataGridCell>()
			.FirstOrDefault(cell => cell.Classes.Contains(CurrentCellPseudoClass));
	}

	private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
	{
		if (e.Row.DataContext is RecipeRowViewModel row
			&& e.Column.Tag is string columnKey
			&& !row.IsApplicable(columnKey))
		{
			e.Cancel = true;

			return;
		}

		IsTextEditing = true;
	}

	private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
	{
		IsTextEditing = false;
	}

	// Click-away rule: a changed (orange) cell clears the moment any OTHER cell is pressed. This is
	// not an edit, so it must run even while the grid is read-only during PLC sync, with no IsReadOnly
	// guard. The clear routes through the surface so the sibling orientation surface clears too.
	private void OnCellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
	{
		var pressedRow = e.Row.DataContext as RecipeRowViewModel;
		var pressedColumnKey = e.Column.Tag as string;

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve(
			_pendingChangedCell,
			pressedRow,
			pressedColumnKey);

		if (cellToClear is { } toClear)
		{
			ViewModel?.ClearChangedByClickAway(toClear.Row, toClear.ColumnKey);
		}

		_pendingChangedCell = newPending;
	}

	internal void FocusGrid()
	{
		RecipeGrid.Focus();
	}

	// Re-applies the surface's selection to the DataGrid. The host calls this when the view
	// becomes active after an orientation flip: the surface received the carried-over selection
	// while the view was flipped away, so the visual selection is stale by then.
	internal void SyncSelectionFromSurface()
	{
		if (ViewModel is null)
		{
			return;
		}

		_syncingSelectionFromSurface = true;
		try
		{
			RecipeGrid.SelectedItems.Clear();
			foreach (var index in ViewModel.SelectedStepIndices)
			{
				if (index >= 0 && index < ViewModel.RecipeRows.Count)
				{
					RecipeGrid.SelectedItems.Add(ViewModel.RecipeRows[index]);
				}
			}
		}
		finally
		{
			_syncingSelectionFromSurface = false;
		}
	}

	private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_syncingSelectionFromSurface || ViewModel is null)
		{
			return;
		}

		var indices = new List<int>();
		foreach (var item in RecipeGrid.SelectedItems)
		{
			if (item is RecipeRowViewModel row)
			{
				var index = ViewModel.RecipeRows.IndexOf(row);
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
			RecipeGrid.SelectedIndex = -1;
			return;
		}

		var index = suggestedIndex.Value;
		if (index < 0 || index >= ViewModel.RecipeRows.Count)
		{
			return;
		}

		RecipeGrid.SelectedIndex = index;
	}

	// ScrollIntoView also brings its column fully into view, so a column already fully shown keeps the horizontal
	// offset and following moves the view vertically only.
	private void ScrollStepIntoView(int stepIndex)
	{
		if (ViewModel is { } surface && stepIndex < surface.RecipeRows.Count)
		{
			RecipeGrid.ScrollIntoView(surface.RecipeRows[stepIndex], FullyShownColumn());
		}
	}

	private DataGridColumn? FullyShownColumn()
	{
		var shownRow = RecipeGrid.GetVisualDescendants().OfType<DataGridRow>().FirstOrDefault(row => row.IsVisible);
		if (shownRow?.GetVisualParent() is not Control rowsViewport)
		{
			return null;
		}

		return RecipeGrid.Columns
			.Where(column => column.IsVisible)
			.FirstOrDefault(column => IsFullyShown(column.GetCellContent(shownRow), rowsViewport));
	}

	private static bool IsFullyShown(Control? cellContent, Control rowsViewport)
	{
		if (cellContent?.FindAncestorOfType<DataGridCell>() is not { } cell
			|| cell.TranslatePoint(default, rowsViewport) is not { } topLeft)
		{
			return false;
		}

		return topLeft.X >= 0 && topLeft.X + cell.Bounds.Width <= rowsViewport.Bounds.Width;
	}

	private void BuildGrid()
	{
		if (_columnsBuilt || ViewModel is null)
		{
			return;
		}

		ViewModel.ColumnBuilder.BuildColumns(RecipeGrid, _comboEditCoordinator);
		_columnsBuilt = true;
	}
}
