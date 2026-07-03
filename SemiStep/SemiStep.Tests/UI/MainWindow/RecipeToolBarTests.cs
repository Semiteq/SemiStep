using System.Linq;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using FluentAssertions;

using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.MainWindow;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Unit")]
public sealed class RecipeToolBarTests : IAsyncLifetime
{
	private static readonly string[] _buttonNames =
	[
		"AddButton", "DeleteButton", "CopyButton", "CutButton", "PasteButton", "UndoButton", "RedoButton"
	];

	private readonly UIFixture _fixture = new();
	private MainWindowViewModel _viewModel = null!;
	private Window? _window;

	public async ValueTask InitializeAsync()
	{
		await _fixture.InitializeAsync();
		_viewModel = _fixture.CreateMainWindowViewModel();
	}

	public ValueTask DisposeAsync()
	{
		_window?.Close();
		_viewModel.Dispose();
		return _fixture.DisposeAsync();
	}

	[AvaloniaFact]
	public void ToolBar_Buttons_BindToTheExistingViewModelCommands()
	{
		var toolBar = ShowToolBar();

		toolBar.FindControl<Button>("AddButton")!.Command
			.Should().BeSameAs(_viewModel.RecipeCommands.AddStepCommand);
		toolBar.FindControl<Button>("DeleteButton")!.Command
			.Should().BeSameAs(_viewModel.RecipeCommands.DeleteStepCommand);
		toolBar.FindControl<Button>("CopyButton")!.Command
			.Should().BeSameAs(_viewModel.Clipboard.CopyStepCommand);
		toolBar.FindControl<Button>("CutButton")!.Command
			.Should().BeSameAs(_viewModel.Clipboard.CutStepCommand);
		toolBar.FindControl<Button>("PasteButton")!.Command
			.Should().BeSameAs(_viewModel.Clipboard.PasteStepCommand);
		toolBar.FindControl<Button>("UndoButton")!.Command
			.Should().BeSameAs(_viewModel.RecipeCommands.UndoCommand);
		toolBar.FindControl<Button>("RedoButton")!.Command
			.Should().BeSameAs(_viewModel.RecipeCommands.RedoCommand);
	}

	[AvaloniaFact]
	public void ToolBar_EveryButton_RendersAnIcon()
	{
		var toolBar = ShowToolBar();

		foreach (var name in _buttonNames)
		{
			var button = toolBar.FindControl<Button>(name)!;
			var content = (StackPanel)button.Content!;
			var image = content.Children.OfType<Image>().FirstOrDefault();

			image.Should().NotBeNull($"button {name} must contain an Image");
			image!.Source.Should().NotBeNull($"button {name} icon source must resolve");
		}
	}

	private RecipeToolBar ShowToolBar()
	{
		var toolBar = new RecipeToolBar { DataContext = _viewModel };
		_window = new Window { Content = toolBar };
		_window.Show();
		Dispatcher.UIThread.RunJobs();

		return toolBar;
	}
}
