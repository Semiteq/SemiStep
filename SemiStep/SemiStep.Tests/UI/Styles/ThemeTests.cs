using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;
using AwesomeAssertions.Execution;

using Xunit;

namespace SemiStep.Tests.UI.Styles;

[Trait("Component", "UI")]
[Trait("Area", "Theme")]
[Trait("Category", "Unit")]
public sealed class ThemeTests
{
	private const string Black = "#000000";
	private const string Secondary = "#818594";
	private const string Disabled = "#A8ADBD";
	private const string Accent = "#3574F0";
	private const string AccentHover = "#3369D6";
	private const string AccentPressed = "#315FBD";
	private const string ForeignForeground = "#FF0000";

	[AvaloniaFact]
	public void TextAndWindow_PaintFromTheComponentKeys()
	{
		var selectable = new SelectableTextBlock { Text = "selectable" };
		var foreignParent = new StackPanel { Children = { selectable } };
		TextElement.SetForeground(foreignParent, new SolidColorBrush(Color.Parse(ForeignForeground)));
		var dimLabel = new TextBlock { Text = "label", IsEnabled = false };
		var window = new Window { Content = Stacked(foreignParent, dimLabel) };
		using var scope = new AssertionScope();
		try
		{
			ShowAndLayOut(window);

			Paints(selectable.Foreground, Black, "TextBlockDefaultForeground");
			dimLabel.Classes.Should().Contain(":disabled");
			Paints(dimLabel.Foreground, Disabled, "TextBlockDisabledForeground");
			Paints(window.Foreground, Black, "WindowDefaultForeground");
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public void DefaultButton_PaintsItsCaptionFromTheComponentKeys()
	{
		var button = new Button { Content = "button" };
		var dimButton = new Button { Content = "button", IsEnabled = false };
		var window = new Window { Content = Stacked(button, dimButton) };
		using var scope = new AssertionScope();
		try
		{
			ShowAndLayOut(window);

			dimButton.Classes.Should().Contain(":disabled");
			Paints(button.Foreground, Accent, "ButtonDefaultPrimaryForeground");
			Paints(SurfaceOf(dimButton).Foreground, Disabled, "ButtonDefaultDisabledForeground");
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public void PrimaryButton_PaintsEveryStateFromTheComponentKeys()
	{
		var button = new Button { Content = "primary", Classes = { "primary" } };
		var window = new Window { Content = Stacked(button) };
		using var scope = new AssertionScope();
		try
		{
			ShowAndLayOut(window);

			var surface = SurfaceOf(button);
			Paints(surface.Background, Accent, "ButtonSolidPrimaryBackground");
			Paints(surface.BorderBrush, Accent, "ButtonSolidPrimaryBorderBrush");

			var center = PointAt(window, button);
			button.Classes.Should().Contain(":pointerover");
			Paints(surface.Background, AccentHover, "ButtonSolidPrimaryPointeroverBackground");
			Paints(surface.BorderBrush, AccentHover, "ButtonSolidPrimaryPointeroverBorderBrush");

			window.MouseDown(center, MouseButton.Left);
			Dispatcher.UIThread.RunJobs();
			button.Classes.Should().Contain(":pressed");
			Paints(surface.Background, AccentPressed, "ButtonSolidPrimaryPressedBackground");
			Paints(surface.BorderBrush, AccentPressed, "ButtonSolidPrimaryPressedBorderBrush");
			window.MouseUp(center, MouseButton.Left);
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public void CheckBox_PaintsEveryStateFromTheComponentKeys()
	{
		var checkedBox = new CheckBox { IsChecked = true, Content = "checked" };
		var uncheckedBox = new CheckBox { IsChecked = false, Content = "unchecked" };
		var dimmed = new CheckBox { IsChecked = false, IsEnabled = false, Content = "disabled" };
		var window = new Window { Content = Stacked(checkedBox, uncheckedBox, dimmed) };
		using var scope = new AssertionScope();
		try
		{
			ShowAndLayOut(window);

			Paints(checkedBox.Foreground, Black, "CheckBoxForeground");
			Paints(BoxOf(checkedBox).Background, Accent, "CheckBoxCheckedDefaultBackground");
			Paints(BoxOf(checkedBox).BorderBrush, Accent, "CheckBoxCheckedDefaultBorderBrush");
			dimmed.Classes.Should().Contain(":disabled");
			Paints(ContentOf(dimmed).Foreground, Disabled, "CheckBoxDisabledForeground");

			var center = PointAt(window, checkedBox);
			checkedBox.Classes.Should().Contain(":pointerover");
			Paints(BoxOf(checkedBox).Background, AccentHover, "CheckBoxCheckedPointeroverBackground");
			Paints(BoxOf(checkedBox).BorderBrush, AccentHover, "CheckBoxCheckedPointeroverBorderBrush");

			window.MouseDown(center, MouseButton.Left);
			Dispatcher.UIThread.RunJobs();
			checkedBox.Classes.Should().Contain(":pressed");
			Paints(BoxOf(checkedBox).Background, AccentPressed, "CheckBoxCheckedPressedBackground");
			Paints(BoxOf(checkedBox).BorderBrush, AccentPressed, "CheckBoxCheckedPressedBorderBrush");
			window.MouseUp(center, MouseButton.Left);

			center = PointAt(window, uncheckedBox);
			uncheckedBox.Classes.Should().Contain(":pointerover");
			Paints(BoxOf(uncheckedBox).BorderBrush, Accent, "CheckBoxPointeroverBorderBrush");

			window.MouseDown(center, MouseButton.Left);
			Dispatcher.UIThread.RunJobs();
			uncheckedBox.Classes.Should().Contain(":pressed");
			Paints(BoxOf(uncheckedBox).BorderBrush, AccentPressed, "CheckBoxPressedBorderBrush");
			window.MouseUp(center, MouseButton.Left);
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public void TextBox_PaintsEveryStateFromTheComponentKeys()
	{
		var box = new TextBox { PlaceholderText = "hint" };
		var focusedBox = new TextBox { Text = "text" };
		var dimBox = new TextBox { Text = "text", IsEnabled = false };
		var window = new Window { Content = Stacked(box, focusedBox, dimBox) };
		using var scope = new AssertionScope();
		try
		{
			ShowAndLayOut(window);
			focusedBox.Focus();
			Dispatcher.UIThread.RunJobs();

			Paints(box.Foreground, Black, "TextBoxForeground");
			Paints(PlaceholderOf(box).Foreground, Secondary, "TextBoxPlaceholderForeground");
			dimBox.Classes.Should().Contain(":disabled");
			Paints(dimBox.Foreground, Disabled, "TextBoxDisabledForeground");
			focusedBox.Classes.Should().Contain(":focus");
			Paints(FocusBorderOf(focusedBox).BorderBrush, Accent, "TextBoxFocusBorderBrush");
		}
		finally
		{
			window.Close();
		}
	}

	[AvaloniaFact]
	public void Menu_PaintsEveryStateFromTheComponentKeys()
	{
		var leaf = new MenuItem { Header = "leaf" };
		var gestured = new MenuItem { Header = "gestured", InputGesture = new KeyGesture(Key.M, KeyModifiers.Control) };
		var dimmed = new MenuItem
		{
			Header = "disabled",
			InputGesture = new KeyGesture(Key.D, KeyModifiers.Control),
			IsEnabled = false
		};
		var top = new MenuItem { Header = "top" };
		top.Items.Add(leaf);
		top.Items.Add(gestured);
		top.Items.Add(dimmed);
		var menu = new Menu();
		menu.Items.Add(top);
		var window = new Window { Content = Stacked(menu) };
		using var scope = new AssertionScope();
		try
		{
			ShowAndLayOut(window);

			top.Open();
			Dispatcher.UIThread.RunJobs();

			top.Classes.Should().Contain(":selected");
			Paints(HeaderOf(top).Foreground, Black, "MenuItemPointeroverForeground");
			Paints(HeaderOf(leaf).Foreground, Black, "MenuItemForeground");
			Paints(GestureOf(gestured).Foreground, Secondary, "MenuItemInputGestureTextForeground");
			dimmed.Classes.Should().Contain(":disabled");
			Paints(HeaderOf(dimmed).Foreground, Disabled, "MenuItemDisabledForeground");
			Paints(GestureOf(dimmed).Foreground, Disabled, "MenuItemDisabledInputGestureTextForeground");
		}
		finally
		{
			top.Close();
			window.Close();
		}
	}

	[AvaloniaFact]
	public void ColorView_PaintsItsAccentFromThePrimaryColor()
	{
		var colorView = new ColorView { SelectedIndex = 2, ColorModel = ColorModel.Rgba };
		var window = new Window { Content = Stacked(colorView) };
		using var scope = new AssertionScope();
		try
		{
			ShowAndLayOut(window);

			var selectedTab = colorView.GetVisualDescendants().OfType<TabItem>().Single(tab => tab.IsSelected);
			var rgb = TemplatePart<RadioButton>(colorView, "RgbRadioButton");
			var hsv = TemplatePart<RadioButton>(colorView, "HsvRadioButton");

			Paints(selectedTab.Foreground, Accent, "ColorViewTabItemSelectedForeground");
			rgb.Classes.Should().Contain(":checked");
			Paints(rgb.Background, Accent, "ColorViewRadioButtonCheckedBackground");
			hsv.Classes.Should().NotContain(":checked");
			Paints(hsv.Foreground, Accent, "ColorViewRadioButtonForeground");
		}
		finally
		{
			window.Close();
		}
	}

	private static StackPanel Stacked(params Control[] children)
	{
		var panel = new StackPanel();
		foreach (var child in children)
		{
			panel.Children.Add(child);
		}

		return panel;
	}

	private static void ShowAndLayOut(Window window)
	{
		window.Show();
		Dispatcher.UIThread.RunJobs();
	}

	private static Point PointAt(Window window, Control control)
	{
		var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)
			?? throw new InvalidOperationException("The control is not in the window's visual tree.");
		window.MouseMove(center, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		return center;
	}

	private static void Paints(IBrush? actual, string expected, string key)
	{
		var brush = actual.Should().BeAssignableTo<ISolidColorBrush>(key).Subject;
		brush.Color.Should().Be(Color.Parse(expected), key);
		brush.Opacity.Should().Be(1, key);
	}

	private static T TemplatePart<T>(Control control, string name)
		where T : Control
	{
		return control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);
	}

	private static ContentPresenter SurfaceOf(Button button)
	{
		return TemplatePart<ContentPresenter>(button, "PART_ContentPresenter");
	}

	private static Border BoxOf(CheckBox check)
	{
		return TemplatePart<Border>(check, "NormalRectangle");
	}

	private static ContentPresenter ContentOf(CheckBox check)
	{
		return TemplatePart<ContentPresenter>(check, "PART_ContentPresenter");
	}

	private static TextBlock PlaceholderOf(TextBox box)
	{
		return box.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == box.PlaceholderText);
	}

	private static Border FocusBorderOf(TextBox box)
	{
		return TemplatePart<Border>(box, "PART_ContentPresenterBorder");
	}

	private static ContentPresenter HeaderOf(MenuItem item)
	{
		return TemplatePart<ContentPresenter>(item, "PART_HeaderPresenter");
	}

	private static TextBlock GestureOf(MenuItem item)
	{
		return TemplatePart<TextBlock>(item, "PART_InputGestureText");
	}
}
