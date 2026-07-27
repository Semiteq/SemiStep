using System.Reactive.Linq;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;

using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Core.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.MainWindow;
using SemiStep.UI.ShutdownService;
using SemiStep.UI.StyleEditor;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.StyleEditor;

[Trait("Component", "UI")]
[Trait("Area", "GridStyleEditor")]
[Trait("Category", "Integration")]
public sealed class GridStyleEditorWindowOwnerRoutingTests : IAsyncLifetime
{
	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private MainWindowView _mainWindow = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
		_mainWindow = new MainWindowView { ViewModel = _viewModel };
		_mainWindow.Show();
		Dispatcher.UIThread.RunJobs();
	}

	public async ValueTask DisposeAsync()
	{
		// A dirty session would make Close() re-open the confirmation dialog,
		// leaking an unresolved headless dialog into teardown.
		_fixture.Session.MarkSaved();
		_mainWindow.Close();
		_viewModel.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void CompleteEditorClose_ExitRequested_CleanOwner_CascadesToOwnerClose()
	{
		var editor = ShowEditorOwnedByMainWindow();

		editor.CompleteEditorClose(true);
		Dispatcher.UIThread.RunJobs();

		editor.IsVisible.Should().BeFalse("the editor always closes itself");
		_mainWindow.IsVisible.Should().BeFalse("a clean owner cascades to close through the guard");
	}

	[AvaloniaFact]
	public void CompleteEditorClose_ExitRequested_DirtyOwner_ShowsGuardAndCancelKeepsOwnerOpen()
	{
		new RecipeTestDriver(_fixture.Session).AddWait(1f);
		var editor = ShowEditorOwnedByMainWindow();

		editor.CompleteEditorClose(true);
		Dispatcher.UIThread.RunJobs();

		editor.IsVisible.Should().BeFalse("the editor closes itself before routing the exit to the owner");
		var dialog = _mainWindow.OwnedWindows.OfType<ExitConfirmationDialog>()
			.Should().ContainSingle("the routed close must run the owner's dirty guard").Subject;

		dialog.Close(ExitConfirmationResult.Cancel);
		// Lets the fire-and-forget OnWindowClosing continuation finish.
		Dispatcher.UIThread.RunJobs();

		_mainWindow.IsVisible.Should().BeTrue("Cancel in the guard must leave the owner open, no silent loss");
	}

	[AvaloniaFact]
	public void CompleteEditorClose_RestartLater_ClosesEditorOnly()
	{
		var editor = ShowEditorOwnedByMainWindow();

		editor.CompleteEditorClose(false);
		Dispatcher.UIThread.RunJobs();

		editor.IsVisible.Should().BeFalse("Restart Later closes the editor");
		_mainWindow.IsVisible.Should().BeTrue("Restart Later must not route an exit to the owner");
	}

	[AvaloniaFact]
	public void CompleteEditorClose_ExitRequested_NullOwner_ClosesEditorWithoutOwnerEffect()
	{
		var editor = new GridStyleEditorWindow();
		editor.Show();
		Dispatcher.UIThread.RunJobs();
		editor.Owner.Should().BeNull("an unowned editor has no owner to route the exit to");

		editor.CompleteEditorClose(true);
		Dispatcher.UIThread.RunJobs();

		editor.IsVisible.Should().BeFalse("the editor still closes itself even with no owner");
		_mainWindow.IsVisible.Should().BeTrue("with no owner captured, the exit intent has nowhere to route");
	}

	[AvaloniaFact]
	public async Task SaveThenExitNow_DrivesRealGlue_CascadesToOwnerClose()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var facade = new GridStyleEditorFacade();
		var loaded = (await facade.Load(tempDir.Path)).Value;
		var viewModel = new GridStyleEditorViewModel(
			facade,
			tempDir.Path,
			loaded,
			NullLogger<GridStyleEditorViewModel>.Instance);

		var editor = new GridStyleEditorWindow { ViewModel = viewModel };
		// Modal show establishes the Owner relationship and fires the WhenActivated glue that
		// subscribes SaveCommand -> OnSaveCompleted.
		_ = editor.ShowDialog(_mainWindow);
		Dispatcher.UIThread.RunJobs();

		// Executing SaveCommand drives the real glue: OnSaveCompleted(true) opens the
		// RestartPromptDialog via ShowDialog<bool> — the exact path a user reaches.
		(await viewModel.SaveCommand.Execute()).Should().BeTrue("the shipped config saves successfully");
		Dispatcher.UIThread.RunJobs();

		var restartDialog = editor.OwnedWindows.OfType<RestartPromptDialog>()
			.Should().ContainSingle("a successful save must raise the restart prompt through the glue").Subject;

		// Exit Now resolves ShowDialog<bool> to true, routing CompleteEditorClose(true).
		restartDialog.Close(true);
		Dispatcher.UIThread.RunJobs();

		editor.IsVisible.Should().BeFalse("the editor closes itself after the restart decision");
		_mainWindow.IsVisible.Should().BeFalse("Exit Now on a clean owner cascades to close through the guard");
	}

	private GridStyleEditorWindow ShowEditorOwnedByMainWindow()
	{
		var editor = new GridStyleEditorWindow();
		// Modal show establishes the real Owner relationship; fired non-awaited (like the
		// exit-flow tests) so the test thread keeps control while the dialog is open.
		_ = editor.ShowDialog(_mainWindow);
		Dispatcher.UIThread.RunJobs();
		return editor;
	}

	private static TempDirectory CopyShippedConfig(string equipment)
	{
		var source = ShippedConfigLocator.GetConfigDirectory(equipment);
		var tempDir = new TempDirectory();
		var uiDir = Path.Combine(tempDir.Path, "ui");
		Directory.CreateDirectory(uiDir);
		File.Copy(
			Path.Combine(source, "ui", "grid_style.yaml"),
			Path.Combine(uiDir, "grid_style.yaml"),
			overwrite: true);
		return tempDir;
	}
}
