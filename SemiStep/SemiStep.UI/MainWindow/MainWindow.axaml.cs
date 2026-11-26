using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;

using ReactiveUI;
using ReactiveUI.Avalonia;

using SemiStep.UI.Plc;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.ShutdownService;
using SemiStep.UI.StyleEditor;

using LocalizationResources = SemiStep.UI.Localization.Resources;

namespace SemiStep.UI.MainWindow;

internal partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
	private bool _forceClose;
	private bool _exitChoiceInProgress;

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

			ViewModel.RecipeFile.UnsavedChangesInteraction
				.RegisterHandler(HandleUnsavedChangesDialogAsync)
				.DisposeWith(disposables);

			ViewModel.ShowStyleEditorInteraction
				.RegisterHandler(HandleStyleEditorDialogAsync)
				.DisposeWith(disposables);

			ViewModel.ResolveConflictInteraction
				.RegisterHandler(HandleResolveConflictDialogAsync)
				.DisposeWith(disposables);

			ViewModel.RequestCloseInteraction
				.RegisterHandler(HandleRequestCloseAsync)
				.DisposeWith(disposables);

			ViewModel.RecipeFile.NewRecipeCommand
				.Merge(ViewModel.RecipeFile.LoadRecipeCommand)
				.Merge(ViewModel.RecipeCommands.DeleteStepCommand)
				.Merge(ViewModel.Clipboard.CutStepCommand)
				.Subscribe(_ => RecipeGridHost.FocusActiveGrid())
				.DisposeWith(disposables);
		});
	}

	/// <summary>Runs the menu item a key no control took names; from a text box only Ctrl+Shift+T runs.</summary>
	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);

		if (MenuBar.ItemFor(e) is not { InputGesture: { } gesture } item)
		{
			return;
		}

		// The orientation flip commits an open cell editor and changes only the view, so it stays
		// live while a text box has focus.
		var fromTextBox = e.Source is TextBox && !gesture.Equals(WindowGestures.ToggleOrientation);
		if (fromTextBox || (WindowGestures.BelongsToCellEditor(gesture) && IsEditingAt(e.Source)))
		{
			return;
		}

		if (item.Command is { } command && command.CanExecute(item.CommandParameter))
		{
			command.Execute(item.CommandParameter);
			e.Handled = true;
		}
	}

	// A canonical combo cell lives in CellTemplate, so its open list is not a DataGrid edit session.
	private bool IsEditingAt(object? source)
	{
		return RecipeGridHost.IsEditing
			|| (source as ILogical)?.GetSelfAndLogicalAncestors().OfType<ComboBox>().Any(combo => combo.IsDropDownOpen) == true;
	}

	private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
	{
		if (_forceClose)
		{
			return;
		}

		// The OS does not honor the cancellation on shutdown; a dialog would only
		// dangle while the process dies.
		if (e.CloseReason == WindowCloseReason.OSShutdown)
		{
			return;
		}

		if (_exitChoiceInProgress)
		{
			e.Cancel = true;

			return;
		}

		if (ViewModel is not { IsDirty: true })
		{
			return;
		}

		e.Cancel = true;

		await ShowExitChoiceAsync();
	}

	internal async Task ShowExitChoiceAsync()
	{
		_exitChoiceInProgress = true;
		try
		{
			var result = await ShowUnsavedChangesDialogAsync();

			await HandleExitChoiceAsync(result);
		}
		catch (Exception ex)
		{
			// Reached from an async void event handler: an unhandled throw here unwinds the
			// dispatcher loop into Program.Main and kills the process. Contain it, report,
			// and keep the window open (e.Cancel is already true at the call site).
			ViewModel?.MessagePanel.ReportError($"{LocalizationResources.ExitFailed}: {ex.Message}");
		}
		finally
		{
			_exitChoiceInProgress = false;
		}
	}

	internal async Task HandleExitChoiceAsync(UnsavedChangesChoice result)
	{
		_exitChoiceInProgress = true;
		try
		{
			if (ViewModel is not null && await ViewModel.RecipeFile.ResolveUnsavedChangesAsync(result))
			{
				_forceClose = true;
				Close();
			}
		}
		finally
		{
			_exitChoiceInProgress = false;
		}
	}

	private Task<UnsavedChangesChoice> ShowUnsavedChangesDialogAsync()
	{
		return new ExitConfirmationDialog().ShowDialog<UnsavedChangesChoice>(this);
	}

	private async Task HandleUnsavedChangesDialogAsync(IInteractionContext<Unit, UnsavedChangesChoice> context)
	{
		context.SetOutput(await ShowUnsavedChangesDialogAsync());
	}

	private async Task HandleOpenFileDialogAsync(IInteractionContext<Unit, string?> context)
	{
		var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = LocalizationResources.OpenRecipeDialogTitle,
			AllowMultiple = false,
			FileTypeFilter =
			[
				new FilePickerFileType(LocalizationResources.RecipeFilesFilter) { Patterns = ["*.csv", "*.recipe"] },
				new FilePickerFileType(LocalizationResources.AllFilesFilter) { Patterns = ["*.*"] }
			]
		});

		var selectedPath = files.Count > 0 ? files[0].Path.LocalPath : null;
		context.SetOutput(selectedPath);
	}

	private async Task HandleSaveFileDialogAsync(IInteractionContext<string?, string?> context)
	{
		var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = LocalizationResources.SaveRecipeDialogTitle,
			DefaultExtension = "csv",
			SuggestedFileName = context.Input ?? "recipe",
			FileTypeChoices =
			[
				new FilePickerFileType(LocalizationResources.CsvFilesFilter) { Patterns = ["*.csv"] },
				new FilePickerFileType(LocalizationResources.RecipeFilesFilter) { Patterns = ["*.recipe"] }
			]
		});

		var selectedPath = file?.Path.LocalPath;
		context.SetOutput(selectedPath);
	}

	private async Task HandleStyleEditorDialogAsync(IInteractionContext<GridStyleEditorViewModel, Unit> context)
	{
		var window = new GridStyleEditorWindow { DataContext = context.Input };
		await window.ShowDialog(this);
		context.SetOutput(Unit.Default);
	}

	private async Task HandleResolveConflictDialogAsync(IInteractionContext<PlcConflictDialogViewModel, bool?> context)
	{
		var dialog = new PlcConflictDialog(context.Input);
		await dialog.ShowDialog(this);
		context.SetOutput(dialog.Confirmed ? dialog.KeepLocal : null);
	}

	private Task HandleRequestCloseAsync(IInteractionContext<Unit, Unit> context)
	{
		Close();
		context.SetOutput(Unit.Default);

		return Task.CompletedTask;
	}
}
