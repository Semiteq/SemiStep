using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

using ReactiveUI;
using ReactiveUI.Avalonia;

using SemiStep.UI.Plc;
using SemiStep.UI.Reactive;
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

			ViewModel.ShowStyleEditorInteraction
				.RegisterHandler(HandleStyleEditorDialogAsync)
				.DisposeWith(disposables);

			ViewModel.ResolveConflictInteraction
				.RegisterHandler(HandleResolveConflictDialogAsync)
				.DisposeWith(disposables);

			ViewModel.RequestCloseInteraction
				.RegisterHandler(HandleRequestCloseAsync)
				.DisposeWith(disposables);
		});
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		if (ViewModel is null)
		{
			base.OnKeyDown(e);

			return;
		}

		// The orientation toggle does not interact with editor semantics, and in the transposed
		// view a plain click can leave an always-live editor focused — so the hotkey stays
		// outside the IsEditing gate.
		if (e.Key == Key.T && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
		{
			ViewModel.ToggleOrientationCommand.ExecuteIfPossible();
			e.Handled = true;

			return;
		}

		if (!RecipeGridHost.IsEditing)
		{
			switch (e.Key)
			{
				case Key.Delete:
					ViewModel.RecipeCommands.DeleteStepCommand.ExecuteIfPossible();
					e.Handled = true;

					return;

				case Key.C when e.KeyModifiers == KeyModifiers.Control:
					ViewModel.Clipboard.CopyStepCommand.ExecuteIfPossible();
					e.Handled = true;

					return;

				case Key.X when e.KeyModifiers == KeyModifiers.Control:
					ViewModel.Clipboard.CutStepCommand.ExecuteIfPossible();
					e.Handled = true;

					return;

				case Key.V when e.KeyModifiers == KeyModifiers.Control:
					ViewModel.Clipboard.PasteStepCommand.ExecuteIfPossible();
					e.Handled = true;

					return;
			}
		}

		base.OnKeyDown(e);
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
			var dialog = new ExitConfirmationDialog();
			var result = await dialog.ShowDialog<ExitConfirmationResult>(this);

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

	internal async Task HandleExitChoiceAsync(ExitConfirmationResult result)
	{
		_exitChoiceInProgress = true;
		try
		{
			switch (result)
			{
				case ExitConfirmationResult.Save:
					await SaveAndCloseOnSuccessAsync();

					break;

				case ExitConfirmationResult.DontSave:
					_forceClose = true;
					Close();

					break;

				case ExitConfirmationResult.Cancel:
					break;
			}
		}
		finally
		{
			_exitChoiceInProgress = false;
		}
	}

	private async Task SaveAndCloseOnSuccessAsync()
	{
		if (ViewModel is null)
		{
			return;
		}

		var saveCommand = ViewModel.RecipeFile.SaveRecipeCommand;

		var saveAlreadyInFlight = await saveCommand.IsExecuting.FirstAsync();
		if (saveAlreadyInFlight)
		{
			return;
		}

		bool saved;
		try
		{
			saved = await saveCommand.Execute();
		}
		catch (Exception)
		{
			// The command's ThrownExceptions pipeline already reports the failure
			// to the message panel; the window must simply stay open.
			return;
		}

		if (saved)
		{
			_forceClose = true;
			Close();
		}
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
