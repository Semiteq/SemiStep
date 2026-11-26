using System.Reactive.Linq;

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

using AwesomeAssertions;

using SemiStep.Tests.UI.Helpers;

using SemiStep.UI.MainWindow;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.ShutdownService;

using Xunit;

using MainWindowView = SemiStep.UI.MainWindow.MainWindow;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Integration")]
public sealed class MainWindowUnsavedChangesTests : IAsyncLifetime
{
	private const int DirtyStepCount = 2;

	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private MainWindowView _window = null!;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_fixture.SeedRecipe(DirtyStepCount);
		_viewModel = _fixture.CreateMainWindowViewModel();
		_viewModel.RecipeGrid.Initialize();
		_window = new MainWindowView { ViewModel = _viewModel };
		_window.Show();
		// The window's own handlers register first, so a test handler registered later wins (LIFO).
		Dispatcher.UIThread.RunJobs();
	}

	public async ValueTask DisposeAsync()
	{
		// A dirty session would make Close() open the confirmation dialog during teardown.
		_fixture.Session.MarkSaved();
		_window.Close();
		_viewModel.Dispose();
		await _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public async Task NewRecipeGesture_Dirty_ShowsDialogAndCancelKeepsRecipe()
	{
		PressKey(PhysicalKey.N, RawInputModifiers.Control | RawInputModifiers.Shift);

		var dialog = _window.OwnedWindows.OfType<ExitConfirmationDialog>()
			.Should().ContainSingle("Ctrl+Shift+N on a dirty recipe must ask first").Subject;
		dialog.Close(UnsavedChangesChoice.Cancel);
		await _viewModel.RecipeFile.NewRecipeCommand.IsExecuting.FirstAsync(executing => !executing);

		_fixture.Coordinator.CurrentRecipe.StepCount.Should().Be(DirtyStepCount, "Cancel keeps the recipe");
		_window.IsVisible.Should().BeTrue("the prompt for a new recipe never closes the window");
	}

	[AvaloniaFact]
	public async Task OpenGesture_Dirty_DontSaveOpensFilePicker()
	{
		var pickerInvocations = 0;
		_viewModel.RecipeFile.OpenFileInteraction.RegisterHandler(context =>
		{
			pickerInvocations++;
			context.SetOutput(null);
		});

		PressKey(PhysicalKey.O, RawInputModifiers.Control);

		var dialog = _window.OwnedWindows.OfType<ExitConfirmationDialog>()
			.Should().ContainSingle("Ctrl+O on a dirty recipe must ask first").Subject;
		pickerInvocations.Should().Be(0, "the file picker waits for the answer");
		dialog.Close(UnsavedChangesChoice.DontSave);
		await _viewModel.RecipeFile.LoadRecipeCommand.IsExecuting.FirstAsync(executing => !executing);

		pickerInvocations.Should().Be(1, "Don't save goes on to the file picker");
		_window.IsVisible.Should().BeTrue("Don't save before an open never closes the window");
	}

	private void PressKey(PhysicalKey key, RawInputModifiers modifiers)
	{
		_window.KeyPressQwerty(key, modifiers);
		Dispatcher.UIThread.RunJobs();
	}
}
