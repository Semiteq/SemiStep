using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

using Avalonia.Controls;

using ReactiveUI;
using ReactiveUI.Avalonia;

namespace SemiStep.UI.RecipeGrid;

public partial class CanonicalRecipeGridView : ReactiveUserControl<CanonicalRecipeGridSurface>
{
	private bool _columnsBuilt;
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

			var selectionRequestsSubscription = new SerialDisposable()
				.DisposeWith(disposables);

			this.WhenAnyValue(x => x.ViewModel)
				.Subscribe(surface =>
				{
					if (surface is null)
					{
						selectionRequestsSubscription.Disposable = null;

						return;
					}

					BuildGrid();
					selectionRequestsSubscription.Disposable =
						surface.SelectionRequests.Subscribe(OnSelectionRequested);
				})
				.DisposeWith(disposables);

			Disposable.Create(() =>
			{
				RecipeGrid.BeginningEdit -= OnBeginningEdit;
				RecipeGrid.CellEditEnded -= OnCellEditEnded;
				RecipeGrid.SelectionChanged -= OnSelectionChanged;
				RecipeGrid.CellPointerPressed -= OnCellPointerPressed;
				_pendingChangedCell = null;
				IsEditing = false;
			}).DisposeWith(disposables);
		});
	}

	public bool IsEditing { get; private set; }

	private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
	{
		if (e.Row.DataContext is RecipeRowViewModel row
			&& e.Column.Tag is string columnKey
			&& !row.IsApplicable(columnKey))
		{
			e.Cancel = true;

			return;
		}

		IsEditing = true;
	}

	private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
	{
		IsEditing = false;
	}

	// Click-away rule: a changed (orange) cell clears the moment any OTHER cell is pressed. This is
	// not an edit, so it must run even while the grid is read-only during PLC sync — no IsReadOnly guard.
	private void OnCellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
	{
		var pressedRow = e.Row.DataContext as RecipeRowViewModel;
		var pressedColumnKey = e.Column.Tag as string;

		var (cellToClear, newPending) = ChangedCellClickResolver.Resolve(
			_pendingChangedCell,
			pressedRow,
			pressedColumnKey);

		if (cellToClear is { } toClear
			&& (ViewModel is null || ViewModel.RecipeRows.Contains(toClear.Row)))
		{
			toClear.Row.ClearChanged(toClear.ColumnKey);
		}

		_pendingChangedCell = newPending;
	}

	private void OnDataGridLoadingRow(object? sender, DataGridRowEventArgs e)
	{
		RecipeRowExecutionClassBinder.BindAll(e.Row);
	}

	private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (ViewModel is null)
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

	private void BuildGrid()
	{
		if (_columnsBuilt || ViewModel is null)
		{
			return;
		}

		ViewModel.ColumnBuilder.BuildColumns(RecipeGrid);
		_columnsBuilt = true;
	}
}
