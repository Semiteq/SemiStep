using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

using AwesomeAssertions;
using AwesomeAssertions.Execution;

using SemiStep.Tests.UI.Helpers;
using SemiStep.UI.Localization;
using SemiStep.UI.MainWindow;

using Xunit;

namespace SemiStep.Tests.UI.MainWindow;

[Trait("Component", "UI")]
[Trait("Area", "MainWindow")]
[Trait("Category", "Unit")]
public sealed class RecipeToolBarTests : IAsyncLifetime
{
	private const string Black = "#000000";
	private const string IconGrey = "#6C707E";
	private const string Disabled = "#A8ADBD";
	private const string Separator = "|";

	private static readonly string[] _buttonNames =
	[
		"OpenButton", "SaveButton", "AddButton", "DeleteButton", "CopyButton", "CutButton", "PasteButton",
		"UndoButton", "RedoButton"
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

		ButtonOf(toolBar, "OpenButton").Command.Should().BeSameAs(_viewModel.RecipeFile.LoadRecipeCommand);
		ButtonOf(toolBar, "SaveButton").Command.Should().BeSameAs(_viewModel.RecipeFile.SaveRecipeCommand);
		ButtonOf(toolBar, "AddButton").Command.Should().BeSameAs(_viewModel.RecipeCommands.AddStepCommand);
		ButtonOf(toolBar, "DeleteButton").Command.Should().BeSameAs(_viewModel.RecipeCommands.DeleteStepCommand);
		ButtonOf(toolBar, "CopyButton").Command.Should().BeSameAs(_viewModel.Clipboard.CopyStepCommand);
		ButtonOf(toolBar, "CutButton").Command.Should().BeSameAs(_viewModel.Clipboard.CutStepCommand);
		ButtonOf(toolBar, "PasteButton").Command.Should().BeSameAs(_viewModel.Clipboard.PasteStepCommand);
		ButtonOf(toolBar, "UndoButton").Command.Should().BeSameAs(_viewModel.RecipeCommands.UndoCommand);
		ButtonOf(toolBar, "RedoButton").Command.Should().BeSameAs(_viewModel.RecipeCommands.RedoCommand);
	}

	[AvaloniaFact]
	public void ToolBar_Buttons_AppearInTheApprovedGroups()
	{
		var toolBar = ShowToolBar();

		var row = toolBar.FindControl<StackPanel>("Buttons")!.Children
			.Select(child => child is Button button ? button.Name : Separator);

		row.Should().Equal(
			"OpenButton", "SaveButton", Separator,
			"AddButton", "DeleteButton", "CopyButton", "CutButton", "PasteButton", Separator,
			"UndoButton", "RedoButton");
	}

	[AvaloniaFact]
	public void ToolBar_EveryButton_RendersAVectorIcon()
	{
		var toolBar = ShowToolBar();

		foreach (var name in _buttonNames)
		{
			var icon = IconOf(ButtonOf(toolBar, name));

			icon.Data.Should().NotBeNull($"button {name} draws its glyph");
			icon.Bounds.Width.Should().Be(16, $"button {name} draws a 16 px glyph");
		}
	}

	[AvaloniaFact]
	public void ToolBar_Tooltips_PrintTheLabelAndTheMenuGestureText()
	{
		var toolBar = ShowToolBar();
		var menuBar = new RecipeMenuBar { DataContext = _viewModel };
		var menuItems = menuBar.GetLogicalDescendants().OfType<MenuItem>().ToList();
		var labels = new Dictionary<string, string>
		{
			["OpenButton"] = Resources.TipOpenRecipe,
			["SaveButton"] = Resources.TipSaveRecipe,
			["AddButton"] = Resources.ContextAddStep,
			["DeleteButton"] = Resources.ContextDeleteStep,
			["CopyButton"] = Resources.ContextCopyStep,
			["CutButton"] = Resources.ContextCutStep,
			["PasteButton"] = Resources.ContextPasteStep,
			["UndoButton"] = Resources.ToolbarUndo,
			["RedoButton"] = Resources.ToolbarRedo
		};
		using var scope = new AssertionScope();

		foreach (var name in _buttonNames)
		{
			var button = ButtonOf(toolBar, name);
			var gesture = menuItems.Single(item => ReferenceEquals(item.Command, button.Command)).InputGesture;

			gesture.Should().NotBeNull($"the menu item behind {name} shows a gesture");
			ToolTip.GetTip(button).Should().Be($"{labels[name]} ({gesture!.ToString("p", null)})", name);
		}
	}

	[AvaloniaFact]
	public void ToolBar_CaptionAndIcon_AreBlackAndGreyAtRest()
	{
		var toolBar = ShowToolBar();
		var add = ButtonOf(toolBar, "AddButton");
		add.Command!.CanExecute(null).Should().BeTrue("precondition: add is available");

		Paints(CaptionOf(add).Foreground, Black, "caption at rest");
		Paints(IconOf(add).Foreground, IconGrey, "icon at rest");
	}

	[AvaloniaFact]
	public void ToolBar_Icon_TurnsBlackOnHover_CaptionStaysBlack()
	{
		var toolBar = ShowToolBar();
		var add = ButtonOf(toolBar, "AddButton");

		var center = add.TranslatePoint(new Point(add.Bounds.Width / 2, add.Bounds.Height / 2), _window!)!.Value;
		_window!.MouseMove(center, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		add.Classes.Should().Contain(":pointerover");
		Paints(IconOf(add).Foreground, Black, "icon on hover");
		Paints(CaptionOf(add).Foreground, Black, "caption on hover");
	}

	[AvaloniaFact]
	public void ToolBar_DisabledButton_PaintsCaptionAndIconPale()
	{
		var toolBar = ShowToolBar();
		var undo = ButtonOf(toolBar, "UndoButton");

		undo.Classes.Should().Contain(":disabled", "precondition: nothing to undo");
		Paints(CaptionOf(undo).Foreground, Disabled, "disabled caption");
		Paints(IconOf(undo).Foreground, Disabled, "disabled icon");
	}

	private static Button ButtonOf(RecipeToolBar toolBar, string name)
	{
		return toolBar.FindControl<Button>(name)!;
	}

	private static PathIcon IconOf(Button button)
	{
		return ((StackPanel)button.Content!).Children.OfType<PathIcon>().Single();
	}

	private static TextBlock CaptionOf(Button button)
	{
		return ((StackPanel)button.Content!).Children.OfType<TextBlock>().Single();
	}

	private static void Paints(IBrush? actual, string expected, string because)
	{
		actual.Should().BeAssignableTo<ISolidColorBrush>(because).Subject.Color.Should().Be(Color.Parse(expected), because);
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
