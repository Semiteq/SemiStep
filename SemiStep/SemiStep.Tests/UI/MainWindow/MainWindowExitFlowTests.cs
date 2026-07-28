using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.ShutdownService;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Integration")]
public sealed class MainWindowExitFlowTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private readonly string _tempFilePath =
		Path.Combine(Path.GetTempPath(), $"SemiStep.ExitFlow.{Guid.NewGuid():N}.csv");
	private MainWindowViewModel _viewModel = null!;
	private MainWindowView _window = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
		_window = new MainWindowView { ViewModel = _viewModel };
		_window.Show();
		// Guarantees the window's own interaction handlers are registered before any
		// test handler, so LIFO resolution favors the test handler.
		Dispatcher.UIThread.RunJobs();
	}

	public async ValueTask DisposeAsync()
	{
		// A dirty session would make Close() re-open the confirmation dialog,
		// leaking an unresolved headless dialog into teardown.
		_fixture.Session.MarkSaved();
		_window.Close();
		_viewModel.Dispose();
		await _fixture.DisposeAsync();
		if (File.Exists(_tempFilePath))
		{
			File.Delete(_tempFilePath);
		}
	}

	[AvaloniaFact]
	public async Task HandleExitChoice_Save_CancelledPicker_KeepsWindowOpen()
	{
		// Registered after Show(): interaction handlers are invoked LIFO, so this
		// handler must win over the window's own file-picker handler.
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(null));

		await _window.HandleExitChoiceAsync(ExitConfirmationResult.Save);

		_window.IsVisible.Should().BeTrue("a cancelled picker means nothing was saved, so the window must stay open");
	}

	[AvaloniaFact]
	public async Task HandleExitChoice_Save_SuccessfulSave_ClosesWindow()
	{
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(_tempFilePath));

		await _window.HandleExitChoiceAsync(ExitConfirmationResult.Save);

		_window.IsVisible.Should().BeFalse("a confirmed save must let the window close");
		File.Exists(_tempFilePath).Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task HandleExitChoice_Save_FailedCoordinatorSave_KeepsWindowOpen()
	{
		var driver = new RecipeTestDriver(_fixture.Session);
		driver.AddFor(3).AddWait(1f);
		_fixture.Session.IsValid.Should().BeFalse("the recipe has an unclosed For loop");
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(context => context.SetOutput(_tempFilePath));

		await _window.HandleExitChoiceAsync(ExitConfirmationResult.Save);

		_window.IsVisible.Should().BeTrue("a failed save must not discard the recipe by closing the window");
		File.Exists(_tempFilePath).Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task HandleExitChoice_Save_SaveThrows_KeepsWindowOpenAndReportsError()
	{
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(
			_ => throw new InvalidOperationException("disk detached"));

		await _window.HandleExitChoiceAsync(ExitConfirmationResult.Save);
		// The ThrownExceptions report is posted to the dispatcher via ObserveOn.
		Dispatcher.UIThread.RunJobs();

		_window.IsVisible.Should().BeTrue("a save exception must not discard the recipe by closing the window");
		var errorEntry = _fixture.MessagePanel.Entries
			.Should().ContainSingle(e => e.Severity == MessageSeverity.Error).Subject;
		errorEntry.Message.Should().StartWith("Save failed:");
		errorEntry.Message.Should().Contain("disk detached");
	}

	[AvaloniaFact]
	public async Task ShowExitChoice_DialogShowThrows_ContainsFaultAndReports()
	{
		// An unshown owner makes ExitConfirmationDialog.ShowDialog(this) throw, standing in for any
		// fault in the async-void closing path. Without the guard the throw escapes the async void
		// event handler and crashes the process.
		var viewModel = _fixture.CreateMainWindowViewModel();
		try
		{
			var window = new MainWindowView { ViewModel = viewModel };

			await window.ShowExitChoiceAsync();

			var errorEntry = viewModel.MessagePanel.Entries
				.Should().ContainSingle(e => e.Severity == MessageSeverity.Error).Subject;
			errorEntry.Message.Should().StartWith("Exit failed:");
		}
		finally
		{
			viewModel.Dispose();
		}
	}

	[AvaloniaFact]
	public async Task HandleExitChoice_DontSave_ClosesWindow()
	{
		await _window.HandleExitChoiceAsync(ExitConfirmationResult.DontSave);

		_window.IsVisible.Should().BeFalse("Don't Save must close the window without saving");
	}

	[AvaloniaFact]
	public async Task HandleExitChoice_Cancel_KeepsWindowOpen()
	{
		await _window.HandleExitChoiceAsync(ExitConfirmationResult.Cancel);

		_window.IsVisible.Should().BeTrue("Cancel must leave the window open");
	}

	[AvaloniaFact]
	public async Task HandleExitChoice_DefaultResult_MeansCancelAndKeepsWindowOpen()
	{
		default(ExitConfirmationResult).Should().Be(
			ExitConfirmationResult.Cancel,
			"dismissing the dialog (title-bar X, Alt+F4) yields default and must not trigger a save");

		await _window.HandleExitChoiceAsync(default);

		_window.IsVisible.Should().BeTrue("dismissing the dialog must behave as Cancel");
	}

	[AvaloniaFact]
	public void WindowClose_CleanSession_Closes()
	{
		_window.Close();

		_window.IsVisible.Should().BeFalse("a clean session needs no confirmation");
	}

	[AvaloniaFact]
	public void WindowClose_DirtySession_IsCancelledAndDialogCancelKeepsWindowOpen()
	{
		new RecipeTestDriver(_fixture.Session).AddWait(1f);

		_window.Close();

		_window.IsVisible.Should().BeTrue("a dirty close must wait for the user's choice");
		var dialog = _window.OwnedWindows.OfType<ExitConfirmationDialog>()
			.Should().ContainSingle("the dirty close must show the confirmation dialog").Subject;

		dialog.Close(ExitConfirmationResult.Cancel);
		// Lets the fire-and-forget OnWindowClosing continuation finish.
		Dispatcher.UIThread.RunJobs();

		_window.IsVisible.Should().BeTrue("Cancel chosen in the dialog must leave the window open");
	}

	[AvaloniaFact]
	public void WindowClose_DirtySession_DialogDontSave_ClosesWindow()
	{
		new RecipeTestDriver(_fixture.Session).AddWait(1f);
		_window.Close();
		var dialog = _window.OwnedWindows.OfType<ExitConfirmationDialog>()
			.Should().ContainSingle("the dirty close must show the confirmation dialog").Subject;

		dialog.Close(ExitConfirmationResult.DontSave);
		// Lets the fire-and-forget OnWindowClosing continuation finish.
		Dispatcher.UIThread.RunJobs();

		_window.IsVisible.Should().BeFalse("Don't Save chosen in the dialog must close the window");
	}

	[AvaloniaFact]
	public void WindowClose_WhileConfirmationDialogOpen_DoesNotStackSecondDialog()
	{
		new RecipeTestDriver(_fixture.Session).AddWait(1f);
		_window.Close();
		var dialog = _window.OwnedWindows.OfType<ExitConfirmationDialog>()
			.Should().ContainSingle("the dirty close must show the confirmation dialog").Subject;

		_window.Close();

		_window.IsVisible.Should().BeTrue("a close during an open confirmation dialog must be rejected");
		_window.OwnedWindows.OfType<ExitConfirmationDialog>()
			.Should().ContainSingle("a re-entrant close must not stack a second confirmation dialog");

		dialog.Close(ExitConfirmationResult.Cancel);
		// Lets the fire-and-forget OnWindowClosing continuation finish.
		Dispatcher.UIThread.RunJobs();
	}

	[AvaloniaFact]
	public async Task WindowClose_WhileExitSaveInFlight_IsCancelled()
	{
		var pickerResult = new TaskCompletionSource<string?>();
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(async context =>
			context.SetOutput(await pickerResult.Task));

		var exitTask = _window.HandleExitChoiceAsync(ExitConfirmationResult.Save);
		_window.Close();

		_window.IsVisible.Should().BeTrue("a close during an in-flight exit save must be rejected");

		pickerResult.SetResult(null);
		await exitTask;

		_window.IsVisible.Should().BeTrue("the deferred picker was cancelled, so nothing was saved");
	}

	[AvaloniaFact]
	public async Task HandleExitChoice_Save_WhileSaveAlreadyInFlight_KeepsWindowOpenWithoutFalseError()
	{
		var pickerGate = new TaskCompletionSource<string?>();
		var pickerInvocations = 0;
		_viewModel.RecipeFile.SaveFileInteraction.RegisterHandler(async context =>
		{
			pickerInvocations++;
			context.SetOutput(await pickerGate.Task);
		});

		// A save started elsewhere (e.g. Ctrl+S) is still running, blocked on the picker.
		var inFlightSave = _viewModel.RecipeFile.SaveRecipeCommand.Execute().ToTask();
		Dispatcher.UIThread.RunJobs();

		await _window.HandleExitChoiceAsync(ExitConfirmationResult.Save);

		_window.IsVisible.Should().BeTrue("an exit save while a save is already running must keep the window open");
		pickerInvocations.Should().Be(1, "the exit flow must not launch a second save while one is in flight");
		_fixture.MessagePanel.Entries.Should().NotContain(
			e => e.Severity == MessageSeverity.Error,
			"re-invoking the busy command must not surface a false save failure");

		pickerGate.SetResult(_tempFilePath);
		(await inFlightSave).Should().BeTrue("the original in-flight save completes on its own");
		Dispatcher.UIThread.RunJobs();
	}

	[AvaloniaFact]
	public async Task ExitCommand_DirtySession_DoesNotCloseAndShowsConfirmation()
	{
		new RecipeTestDriver(_fixture.Session).AddWait(1f);

		await _viewModel.ExitCommand.Execute();

		_window.IsVisible.Should().BeTrue("File > Exit on a dirty session must not close without confirmation");
		var dialog = _window.OwnedWindows.OfType<ExitConfirmationDialog>()
			.Should().ContainSingle("the dirty Exit must show the confirmation dialog").Subject;

		dialog.Close(ExitConfirmationResult.Cancel);
		// Lets the fire-and-forget OnWindowClosing continuation finish and dismisses the dialog
		// so it does not dangle into teardown.
		Dispatcher.UIThread.RunJobs();

		_window.IsVisible.Should().BeTrue("Cancel chosen in the dialog must leave the window open");
	}

	[AvaloniaFact]
	public async Task ExitCommand_CleanSession_ClosesWindow()
	{
		await _viewModel.ExitCommand.Execute();

		_window.IsVisible.Should().BeFalse("File > Exit on a clean session must close the window");
	}
}
