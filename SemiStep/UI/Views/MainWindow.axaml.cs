using System.Reactive;
using System.Reactive.Disposables;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;

using ReactiveUI;

using UI.Helpers;
using UI.Models;
using UI.ViewModels;

namespace UI.Views;

internal partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
	private ColumnBuilder? _columnBuilder;
	private bool _forceClose;
	private bool _isEditing;

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

			ViewModel.Clipboard.SetClipboard(Clipboard);

			ViewModel.RecipeFile.OpenFileInteraction
				.RegisterHandler(HandleOpenFileDialogAsync)
				.DisposeWith(disposables);

			ViewModel.RecipeFile.SaveFileInteraction
				.RegisterHandler(HandleSaveFileDialogAsync)
				.DisposeWith(disposables);

			ViewModel.ShowMessageInteraction
				.RegisterHandler(HandleShowMessageAsync)
				.DisposeWith(disposables);

			_columnBuilder = new ColumnBuilder(
				ViewModel.RecipeGrid.ActionRegistry,
				ViewModel.RecipeGrid.GroupRegistry,
				ViewModel.Configuration.GridStyle,
				ViewModel.RecipeGrid.PropertyRegistry,
				ViewModel.RecipeGrid.ColumnRegistry);
			BuildGrid();

			RecipeGrid.BeginningEdit += OnBeginningEdit;
			RecipeGrid.CellEditEnded += OnCellEditEnded;
			RecipeGrid.SelectionChanged += OnSelectionChanged;

			Disposable.Create(() =>
			{
				RecipeGrid.BeginningEdit -= OnBeginningEdit;
				RecipeGrid.CellEditEnded -= OnCellEditEnded;
				RecipeGrid.SelectionChanged -= OnSelectionChanged;
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
					ViewModel.RecipeGrid.DeleteStepCommand.Execute().Subscribe();
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
		_isEditing = true;
	}

	private void OnCellEditEnded(object? sender, DataGridCellEditEndedEventArgs e)
	{
		_isEditing = false;
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

	private async Task HandleShowMessageAsync(IInteractionContext<(string Title, string Message), Unit> context)
	{
		var (title, message) = context.Input;
		var dialog = new MessageDialog(title, message);
		await dialog.ShowDialog(this);
		context.SetOutput(Unit.Default);
	}

	private void BuildGrid()
	{
		if (_columnBuilder is null || ViewModel is null)
		{
			return;
		}

		_columnBuilder.BuildColumnsFromConfiguration(RecipeGrid, ViewModel.Configuration);
	}
}
