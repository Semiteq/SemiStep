using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

using ReactiveUI;
using ReactiveUI.Avalonia;

using SemiStep.UI.ShutdownService;

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

			ViewModel.MainWindow = this;
			ViewModel.Clipboard.SetClipboard(Clipboard);

			ViewModel.RecipeFile.OpenFileInteraction
				.RegisterHandler(HandleOpenFileDialogAsync)
				.DisposeWith(disposables);

			ViewModel.RecipeFile.SaveFileInteraction
				.RegisterHandler(HandleSaveFileDialogAsync)
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
			ViewModel.ToggleOrientationCommand.Execute().Subscribe();
			e.Handled = true;

			return;
		}

		if (!RecipeGridHost.IsEditing)
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

		_exitChoiceInProgress = true;
		try
		{
			var dialog = new ExitConfirmationDialog();
			var result = await dialog.ShowDialog<ExitConfirmationResult>(this);

			await HandleExitChoiceAsync(result);
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
}
