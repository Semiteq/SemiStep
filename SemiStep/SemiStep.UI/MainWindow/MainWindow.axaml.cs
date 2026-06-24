using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

using ReactiveUI;
using ReactiveUI.Avalonia;

using SemiStep.UI.RecipeGrid;
using SemiStep.UI.ShutdownService;

namespace SemiStep.UI.MainWindow;

internal partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
	private ColumnBuilder? _columnBuilder;
	private bool _forceClose;
	private bool _isEditing;
	private bool _columnsBuilt;
	private (RecipeRowViewModel Row, string ColumnKey)? _pendingChangedCell;

	public MainWindow()
	{
		InitializeComponent();

		Closing += OnWindowClosing;

		this.WhenActivated(disposables =>
		{
			if (ViewModel is null)
			{
				return;
			}

			ViewModel.MainWindow = this;
			ViewModel.Clipboard.SetClipboard(Clipboard);

			ViewModel.RecipeFile.OpenFileInteraction
				.RegisterHandler(HandleOpenFileDialogAsync)
				.DisposeWith(disposables);

			ViewModel.RecipeFile.SaveFileInteraction
				.RegisterHandler(HandleSaveFileDialogAsync)
				.DisposeWith(disposables);

			_columnBuilder = ViewModel.ColumnBuilder;
			BuildGrid();

			RecipeGrid.BeginningEdit += OnBeginningEdit;
			RecipeGrid.CellEditEnded += OnCellEditEnded;
			RecipeGrid.SelectionChanged += OnSelectionChanged;
			RecipeGrid.CellPointerPressed += OnCellPointerPressed;
			ViewModel.RecipeGrid.SelectionRequested += OnSelectionRequested;

			Disposable.Create(() =>
			{
				RecipeGrid.BeginningEdit -= OnBeginningEdit;
				RecipeGrid.CellEditEnded -= OnCellEditEnded;
				RecipeGrid.SelectionChanged -= OnSelectionChanged;
				RecipeGrid.CellPointerPressed -= OnCellPointerPressed;
				_pendingChangedCell = null;
				if (ViewModel is not null)
				{
					ViewModel.RecipeGrid.SelectionRequested -= OnSelectionRequested;
				}
			}).DisposeWith(disposables);
		});
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (ViewModel is null)
		{
			base.OnKeyDown(e);

			return;
		}

		if (!_isEditing)
		{
			switch (e.Key)
			{
				case Key.Delete:
					ViewModel.RecipeCommands.DeleteStepCommand.Execute().Subscribe();
					e.Handled = true;

					return;

				case Key.C when e.KeyModifiers == KeyModifiers.Control:
					ViewModel.Clipboard.CopyStepCommand.Execute().Subscribe();
					e.Handled = true;

					return;

				case Key.X when e.KeyModifiers == KeyModifiers.Control:
					ViewModel.Clipboard.CutStepCommand.Execute().Subscribe();
					e.Handled = true;

					return;

				case Key.V when e.KeyModifiers == KeyModifiers.Control:
					ViewModel.Clipboard.PasteStepCommand.Execute().Subscribe();
					e.Handled = true;

					return;
			}
		}

		base.OnKeyDown(e);
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

		_isEditing = true;
	}

	private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
	{
		_isEditing = false;
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
			&& (ViewModel is null || ViewModel.RecipeGrid.RecipeRows.Contains(toClear.Row)))
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
				var index = ViewModel.RecipeGrid.RecipeRows.IndexOf(row);
				if (index >= 0)
				{
					indices.Add(index);
				}
			}
		}

		indices.Sort();
		ViewModel.RecipeGrid.SelectedRowIndices = indices;
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
		if (index < 0 || index >= ViewModel.RecipeGrid.RecipeRows.Count)
		{
			return;
		}

		RecipeGrid.SelectedIndex = index;
	}

	private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
	{
		if (_forceClose)
		{
			return;
		}

		if (ViewModel is not { IsDirty: true })
		{
			return;
		}

		e.Cancel = true;

		var dialog = new ExitConfirmationDialog();
		var result = await dialog.ShowDialog<ExitConfirmationResult>(this);

		switch (result)
		{
			case ExitConfirmationResult.Save:
				ViewModel.RecipeFile.SaveRecipeCommand.Execute().Subscribe(_ =>
				{
					_forceClose = true;
					Close();
				});

				break;

			case ExitConfirmationResult.DontSave:
				_forceClose = true;
				Close();

				break;

			case ExitConfirmationResult.Cancel:
				break;
		}
	}

	private async Task HandleOpenFileDialogAsync(IInteractionContext<Unit, string?> context)
	{
		var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = "Open Recipe",
			AllowMultiple = false,
			FileTypeFilter =
			[
				new FilePickerFileType("Recipe Files") { Patterns = ["*.csv", "*.recipe"] },
				new FilePickerFileType("All Files") { Patterns = ["*.*"] }
			]
		});

		var selectedPath = files.Count > 0 ? files[0].Path.LocalPath : null;
		context.SetOutput(selectedPath);
	}

	private async Task HandleSaveFileDialogAsync(IInteractionContext<string?, string?> context)
	{
		var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = "Save Recipe",
			DefaultExtension = "csv",
			SuggestedFileName = context.Input ?? "recipe",
			FileTypeChoices =
			[
				new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] },
				new FilePickerFileType("Recipe Files") { Patterns = ["*.recipe"] }
			]
		});

		var selectedPath = file?.Path.LocalPath;
		context.SetOutput(selectedPath);
	}

	private void BuildGrid()
	{
		if (_columnsBuilt || _columnBuilder is null || ViewModel is null)
		{
			return;
		}

		_columnBuilder.BuildColumns(RecipeGrid);
		_columnsBuilt = true;
	}
}
