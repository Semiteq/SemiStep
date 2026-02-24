using System.Reactive;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using Microsoft.Extensions.DependencyInjection;

using ReactiveUI;

using UI.Helpers;
using UI.Services;
using UI.ViewModels;

namespace UI.Views;

public partial class MainWindow : Window
{
	private ColumnBuilder? _columnBuilder;
	private bool _forceClose;

	public MainWindow()
	{
		InitializeComponent();
		DataContextChanged += OnDataContextChanged;
		Closing += OnWindowClosing;
	}

	private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
	{
		if (_forceClose)
		{
			return;
		}

		if (DataContext is not MainWindowViewModel viewModel || !viewModel.IsDirty)
		{
			return;
		}

		// Cancel the close and show confirmation dialog
		e.Cancel = true;

		var dialog = new ExitConfirmationDialog();
		await dialog.ShowDialog(this);

		switch (dialog.Result)
		{
			case ExitConfirmationResult.Save:
				viewModel.SaveRecipeCommand.Execute().Subscribe(_ =>
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
				// Do nothing, stay open
				break;
		}
	}

	private void OnDataContextChanged(object? sender, EventArgs e)
	{
		if (DataContext is not MainWindowViewModel viewModel || viewModel.Configuration is null)
		{
			return;
		}

		// Initialize toast notification manager
		var notificationService = App.ServiceProvider?.GetService<NotificationService>();
		notificationService?.SetHostWindow(this);

		// Register file dialog interaction handlers
		viewModel.OpenFileInteraction.RegisterHandler(HandleOpenFileDialogAsync);
		viewModel.SaveFileInteraction.RegisterHandler(HandleSaveFileDialogAsync);
		viewModel.ShowMessageInteraction.RegisterHandler(HandleShowMessageAsync);

		_columnBuilder = new ColumnBuilder(
			viewModel.ActionRegistry);

		BuildGrid();
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
		if (_columnBuilder is null || DataContext is not MainWindowViewModel viewModel ||
			viewModel.Configuration is null)
		{
			return;
		}

		_columnBuilder.BuildColumnsFromConfiguration(RecipeGrid, viewModel.Configuration);
	}

	private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (DataContext is not MainWindowViewModel viewModel)
		{
			return;
		}

		// Update row selection state for custom styling
		foreach (var row in viewModel.RecipeRows)
		{
			row.IsSelected = RecipeGrid.SelectedItems?.Contains(row) ?? false;
		}
	}
}
