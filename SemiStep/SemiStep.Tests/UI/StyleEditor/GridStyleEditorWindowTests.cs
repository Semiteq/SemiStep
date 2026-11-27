using System.Globalization;
using System.Reactive.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;
using SemiStep.UI.StyleEditor;
using SemiStep.UI.Styles;

using Xunit;

namespace SemiStep.Tests.UI.StyleEditor;

[Trait("Component", "UI")]
[Trait("Category", "Integration")]
[Trait("Area", "GridStyleEditor")]
public sealed class GridStyleEditorWindowTests
{
	private const double ListLeft = 16;
	private const double LabelLeft = 16 + 176 + 16;
	private const double FieldLeft = LabelLeft + 152 + 12;

	private static readonly string[] _editorControlNames =
	[
		"FontsFontFamily", "FontsHeaderFontSize", "FontsHeaderFontWeight", "FontsHeaderItalic",
		"FontsCellFontSize", "FontsCellFontWeight", "FontsCellItalic",
		"StatusBarFontSize", "StatusBarWeight", "StatusBarItalic",
		"LayoutRowHeight", "LayoutCellPaddingLeft", "LayoutCellPaddingTop", "LayoutCellPaddingRight",
		"LayoutCellPaddingBottom", "ChromeGridBackground", "ChromeGridBorder", "ChromeGridLine",
		"ChromeHeaderForeground",
		"ChangedCellsChanged", "GreyCellsReadOnly", "GreyCellsDisabled", "SelectionAccent", "SelectionWeight",
		"ExecutionRunning", "ExecutionRunningWeight", "ExecutionPastWeight", "LoopColor", "LoopDepth1Weight",
		"LoopDepth2Weight", "LoopDepth3Weight",
		"StatusBarPadding", "StatusBarBackground", "StatusBarForeground", "ChromeSecondaryForeground",
		"ChromeConnected", "ChromeDisconnected", "ChromeSeparator", "ChromeSubtleBorder",
		"ValidationPanelMaxHeight", "ValidationPanelBackground", "ChromePanelHeaderBackground",
		"ValidationPanelForeground", "ValidationPanelErrorColor", "ValidationPanelWarningColor"
	];

	[AvaloniaFact]
	public async Task Window_IsAFixedDialog_ThatOpensOnTheFontsPage()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;

		window.ClientSize.Should().Be(new Size(756, 560));
		window.CanResize.Should().BeFalse();
		window.ShowInTaskbar.Should().BeFalse();
		Control(window, "SectionList").Should().BeOfType<ListBox>().Which.SelectedIndex.Should().Be(0);
		VisiblePage(window).Name.Should().Be("FontsPage");
	}

	[AvaloniaFact]
	public async Task SectionListAndPages_FillTheBody_AndTheFooterHasItsDesignedHeight()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		var list = (ListBox)Control(window, "SectionList");
		var pages = Control(window, "Pages");

		BoundsIn(window, list).Should().Be(new Rect(ListLeft, 16, 176, 415));
		BoundsIn(window, pages).Should().Be(new Rect(LabelLeft, 16, 532, 415));
		Control(window, "MessageLine").Bounds.Height.Should().Be(40);
		Control(window, "SaveButton").Bounds.Height.Should().Be(32);
		pages.GetVisualAncestors().OfType<ScrollViewer>().Should().BeEmpty("the form never scrolls");

		var listScroll = list.GetVisualDescendants().OfType<ScrollViewer>().Single();
		listScroll.Extent.Height.Should().BeLessThanOrEqualTo(listScroll.Viewport.Height);
	}

	[AvaloniaTheory]
	[InlineData(0, "FontsPage", 176)]
	[InlineData(1, "GridPage", 408)]
	[InlineData(2, "CellColorsPage", 248)]
	[InlineData(3, "HighlightPage", 352)]
	[InlineData(4, "StatusBarPage", 368)]
	[InlineData(5, "NotificationPanelPage", 288)]
	public async Task EveryPage_HasTheDesignedHeight_AndOneLabelColumn(int index, string pageName, double height)
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;

		var page = ShowPage(window, index);

		page.Name.Should().Be(pageName);
		page.Bounds.Height.Should().Be(height);
		page.ColumnDefinitions[0].ActualWidth.Should().Be(152);
		page.ColumnDefinitions[2].ActualWidth.Should().Be(344);
		foreach (var label in page.Children.OfType<TextBlock>().Where(text => text.Classes.Contains("field-label")))
		{
			BoundsIn(window, label).X.Should().Be(LabelLeft, $"label '{label.Text}' sits in the label column");
		}
	}

	[AvaloniaFact]
	public async Task Controls_HaveTheirDesignedWidths_AndShareOneLeftEdge()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;

		BoundsIn(window, Control(window, "FontsFontFamily")).Should().Be(new Rect(FieldLeft, 16, 240, 32));
		BoundsIn(window, Control(window, "FontsHeaderFontSize")).Should().Match<Rect>(rect =>
			rect.X == FieldLeft && rect.Width == 120 && rect.Height == 32);
		BoundsIn(window, Control(window, "FontsHeaderFontWeight")).Should().Match<Rect>(rect =>
			rect.X == FieldLeft + 128 && rect.Width == 160 && rect.Height == 32);
		BoundsIn(window, Control(window, "FontsHeaderItalic")).X.Should().Be(FieldLeft + 296);

		ShowPage(window, 1);
		BoundsIn(window, Control(window, "LayoutRowHeight")).Should().Match<Rect>(rect =>
			rect.X == FieldLeft && rect.Width == 120 && rect.Height == 32);
		BoundsIn(window, Picker(window, "ChromeGridBackground")).Should().Match<Rect>(rect =>
			rect.X == FieldLeft && rect.Width == 344 && rect.Height == 32);

		ShowPage(window, 2);
		BoundsIn(window, Control(window, "SelectionWeight")).Should().Match<Rect>(rect =>
			rect.X == FieldLeft && rect.Width == 120 && rect.Height == 32);
	}

	[AvaloniaFact]
	public async Task EveryEditableKey_HasExactlyOneControl_AndTheDroppedRowsAreGone()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;

		var names = new List<string>();
		for (var index = 0; index < 6; index++)
		{
			names.AddRange(ShowPage(window, index).GetLogicalDescendants()
				.OfType<Control>()
				.Where(control => control is NumericUpDown or ComboBox or CheckBox or ColorFieldView)
				.Select(control => control.Name ?? "(unnamed)"));
		}

		names.Should().BeEquivalentTo(_editorControlNames);

		var boundColors = window.GetLogicalDescendants()
			.OfType<ColorFieldView>()
			.Select(field => field.DataContext)
			.ToList();
		var colorDrafts = DraftLeaves<ColorDraft>(editor.ViewModel);
		boundColors.Should().HaveCount(colorDrafts.Count);
		colorDrafts.Should().OnlyContain(draft => boundColors.Count(bound => ReferenceEquals(bound, draft)) == 1);
	}

	[AvaloniaFact]
	public async Task EveryControl_WritesItsOwnKey()
	{
		using var editor = await ShownEditor.OpenAsync(loaded => loaded with
		{
			Fonts = loaded.Fonts with { FontFamily = "Seeded Family" }
		});
		var window = editor.Window;
		var cases = WiringCases();
		cases.Select(wiring => wiring.Control).Should().BeEquivalentTo(_editorControlNames);

		var expected = editor.Loaded;
		foreach (var wiring in cases)
		{
			ShowPage(window, wiring.Page);
			wiring.Change(window);
			Dispatcher.UIThread.RunJobs();
			expected = wiring.Expect(expected);

			editor.ViewModel.BuildRecord().Should().Be(expected, $"{wiring.Control} writes only its own key");
		}

		foreach (var wiring in cases.Where(wiring => wiring.Picked is not null))
		{
			ShowPage(window, wiring.Page);
			var picker = Picker(window, wiring.Control);
			picker.Color.Should().Be(wiring.Picked, $"{wiring.Control} shows the colour of the key it writes");
			SwatchColour(picker).Should().Be(wiring.Picked, $"the {wiring.Control} swatch shows the same colour");
		}
	}

	[AvaloniaFact]
	public async Task EmptiedNumber_ShowsErrorBorderAndGlyph_MarksItsSection_AndDisablesSave()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		ShowPage(window, 1);
		var rowHeight = (NumericUpDown)Control(window, "LayoutRowHeight");

		rowHeight.Value = null;
		Dispatcher.UIThread.RunJobs();

		editor.ViewModel.Layout.RowHeight.Value.Should().BeNull();
		ContentBorderColour(rowHeight.GetVisualDescendants().OfType<TextBox>().First())
			.Should().Be(editor.ErrorColour);
		rowHeight.InnerLeftContent.Should().NotBeNull("an invalid number carries the warning glyph");
		SectionGlyph(window, 1).IsVisible.Should().BeTrue();
		SectionGlyph(window, 0).IsVisible.Should().BeFalse();
		var message = (TextBlock)Control(window, "MessageLine");
		message.Text.Should().Be(editor.ViewModel.ValidationMessage).And.NotBeNullOrEmpty();
		((ISolidColorBrush)message.Foreground!).Color.Should().Be(editor.ErrorColour);
		Control(window, "SaveButton").IsEffectivelyEnabled.Should().BeFalse();
	}

	[AvaloniaTheory]
	[InlineData("ru-RU", "12.5")]
	[InlineData("ru-RU", "12,5")]
	[InlineData("en-US", "12.5")]
	[InlineData("en-US", "12,5")]
	public async Task PercentTyped_EitherDecimalSeparator_StoresTheValue(string culture, string typed)
	{
		using var _ = new CultureScope(culture);
		using var editor = await ShownEditor.OpenAsync();
		ShowPage(editor.Window, 2);

		var box = TypeInto(editor.Window, "SelectionWeight", typed);

		editor.ViewModel.Selection.Weight.Value.Should().Be(0.125m);
		box.Text.Should().Be("12.5");
	}

	[AvaloniaTheory]
	[InlineData("ru-RU", "28.5")]
	[InlineData("ru-RU", "28,5")]
	[InlineData("en-US", "28.5")]
	[InlineData("en-US", "28,5")]
	public async Task WholeNumberTyped_EitherDecimalSeparator_RoundsToAWholeNumber(string culture, string typed)
	{
		using var _ = new CultureScope(culture);
		using var editor = await ShownEditor.OpenAsync();
		ShowPage(editor.Window, 1);

		var box = TypeInto(editor.Window, "LayoutRowHeight", typed);

		editor.ViewModel.Layout.RowHeight.Value.Should().Be(29);
		box.Text.Should().Be("29");
	}

	[AvaloniaTheory]
	[InlineData("ru-RU", "1.234,5")]
	[InlineData("en-US", "1,234.5")]
	public async Task NumberTyped_WithBothSeparators_KeepsTheValue(string culture, string typed)
	{
		using var _ = new CultureScope(culture);
		using var editor = await ShownEditor.OpenAsync();
		ShowPage(editor.Window, 1);
		var seeded = editor.ViewModel.Layout.RowHeight.Value;

		var box = TypeInto(editor.Window, "LayoutRowHeight", typed);

		editor.ViewModel.Layout.RowHeight.Value.Should().Be(seeded);
		box.Text.Should().Be(seeded?.ToString("0", CultureInfo.InvariantCulture));
	}

	[AvaloniaTheory]
	[InlineData(1, "LayoutRowHeight", "500", 500)]
	[InlineData(1, "LayoutCellPaddingLeft", "-3", -3)]
	[InlineData(2, "SelectionWeight", "500", 5)]
	public async Task NumberTypedOutOfRange_IsKept_AndShowsTheRangeMessage(
		int page,
		string controlName,
		string typed,
		decimal stored)
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		ShowPage(window, page);

		var box = TypeInto(window, controlName, typed);

		var draft = NumberDraftOf(editor.ViewModel, controlName);
		draft.Value.Should().Be(stored, "the typed value is not discarded");
		box.Text.Should().Be(typed);
		Control(window, controlName).Classes.Should().Contain("invalid");
		SectionGlyph(window, page).IsVisible.Should().BeTrue();
		((TextBlock)Control(window, "MessageLine")).Text.Should().Be(editor.ViewModel.ValidationMessage)
			.And.Contain(draft == editor.ViewModel.Selection.Weight ? "100" : draft.Maximum.ToString(CultureInfo.CurrentCulture));
		Control(window, "SaveButton").IsEffectivelyEnabled.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task OutOfRangeSeed_StaysInTheDraft_AndShowsAsInvalid_AfterAVisit()
	{
		using var editor = await ShownEditor.OpenAsync(
			editYaml: yaml => yaml.Replace("row_height: 28", "row_height: 500", StringComparison.Ordinal));
		var window = editor.Window;
		var before = await File.ReadAllBytesAsync(editor.FilePath, TestContext.Current.CancellationToken);
		editor.Loaded.Layout.RowHeight.Should().Be(500, "the fixture edits the YAML file");

		for (var index = 0; index < 6; index++)
		{
			foreach (var spinner in ShowPage(window, index).GetLogicalDescendants().OfType<NumericUpDown>())
			{
				spinner.GetVisualDescendants().OfType<TextBox>().First().Focus();
				Dispatcher.UIThread.RunJobs();
			}
		}

		Control(window, "SectionList").Focus();
		Dispatcher.UIThread.RunJobs();

		editor.ViewModel.Layout.RowHeight.Value.Should().Be(500, "visiting the page writes no clamp");
		editor.ViewModel.Layout.RowHeight.IsValid.Should().BeFalse();
		var rowHeight = (NumericUpDown)Control(window, "LayoutRowHeight");
		rowHeight.Value.Should().Be(500, "the field shows the value the draft holds");
		rowHeight.GetVisualDescendants().OfType<TextBox>().First().Text.Should().Be("500");
		rowHeight.Classes.Should().Contain("invalid");
		SectionGlyph(window, 1).IsVisible.Should().BeTrue();
		(await editor.ViewModel.SaveCommand.CanExecute.FirstAsync()).Should().BeFalse();
		(await File.ReadAllBytesAsync(editor.FilePath, TestContext.Current.CancellationToken)).Should().Equal(before);
	}

	[AvaloniaFact]
	public async Task HexTypedInTheFlyout_ReachesTheDraft_AndTheYamlAsUpperCaseRrggbb()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		ShowPage(window, 1);
		var picker = Picker(window, "ChromeGridBackground");
		var flyout = OpenFlyout(window, picker);
		var tabs = ((Control)flyout.Content!).GetVisualDescendants().OfType<TabControl>().Single();
		tabs.SelectedIndex = tabs.ItemCount - 1;
		Dispatcher.UIThread.RunJobs();
		var hexBox = tabs.GetVisualDescendants()
			.OfType<TextBox>()
			.Single(box => box.Name == "PART_HexTextBox");

		hexBox.Focus();
		hexBox.Text = "a1b2c3";
		TopLevel.GetTopLevel(hexBox)!.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
		flyout.Hide();
		Dispatcher.UIThread.RunJobs();

		editor.ViewModel.Chrome.GridBackground.Color.Should().Be(Color.Parse("#A1B2C3"));
		(await editor.ViewModel.SaveCommand.Execute()).Should().BeTrue();
		(await File.ReadAllTextAsync(editor.FilePath, TestContext.Current.CancellationToken))
			.Should().Contain("grid_background: \"#A1B2C3\"");
	}

	[AvaloniaFact]
	public async Task OpeningAndDismissingTheFlyout_KeepsATranslucentSeed()
	{
		using var editor = await ShownEditor.OpenAsync(loaded => loaded with
		{
			Chrome = loaded.Chrome with { GridBackground = StyleColor.Parse("#80123456") }
		});
		var window = editor.Window;
		ShowPage(window, 1);

		OpenFlyout(window, Picker(window, "ChromeGridBackground")).Hide();
		Dispatcher.UIThread.RunJobs();

		editor.ViewModel.BuildRecord().Should().Be(editor.Loaded);
	}

	[AvaloniaFact]
	public async Task Save_KeepsTheWindowOpen_ShowsTheRestartNotice_AndWritesTheYaml()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		ShowPage(window, 1);
		PickColor(window, "ChromeGridBackground", Color.Parse("#123456"));

		(await editor.ViewModel.SaveCommand.Execute()).Should().BeTrue();
		Dispatcher.UIThread.RunJobs();

		window.IsVisible.Should().BeTrue("Save keeps the window open");
		var message = (TextBlock)Control(window, "MessageLine");
		message.Text.Should().Be(editor.ViewModel.Message).And.NotBeNullOrEmpty();
		editor.ViewModel.IsRestartPending.Should().BeTrue();
		((ISolidColorBrush)message.Foreground!).Color.Should().NotBe(editor.ErrorColour);
		(await editor.Reload()).Chrome.GridBackground.Should().Be(StyleColor.Parse("#123456"));
	}

	[AvaloniaFact]
	public async Task RestartNotice_SurvivesAPageVisit_AndGoesOnTheNextEdit()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		ShowPage(window, 1);
		PickColor(window, "ChromeGridBackground", Color.Parse("#123456"));
		await editor.ViewModel.SaveCommand.Execute();
		var message = (TextBlock)Control(window, "MessageLine");

		for (var index = 0; index < 6; index++)
		{
			ShowPage(window, index);
		}

		message.Text.Should().NotBeNullOrEmpty("visiting the pages changes no value");

		ShowPage(window, 0);
		var italic = (CheckBox)Control(window, "FontsCellItalic");
		italic.IsChecked = italic.IsChecked != true;
		Dispatcher.UIThread.RunJobs();

		message.Text.Should().BeNullOrEmpty("the edit is not saved yet");
	}

	[AvaloniaFact]
	public async Task PercentField_ShowsPercent_AndWritesZeroToOneToTheYaml()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		ShowPage(window, 2);
		var weight = (NumericUpDown)Control(window, "SelectionWeight");

		weight.Value.Should().Be((decimal)editor.Loaded.Selection.Weight * 100);

		weight.Value = 12.5m;
		(await editor.ViewModel.SaveCommand.Execute()).Should().BeTrue();

		editor.ViewModel.Selection.Weight.Value.Should().Be(0.125m);
		(await editor.Reload()).Selection.Weight.Should().Be(0.125);
	}

	[AvaloniaFact]
	public async Task VisitingEveryPageWithoutTyping_ChangesNothing_AndSaveWritesNothing()
	{
		using var editor = await ShownEditor.OpenAsync(loaded => loaded with
		{
			Selection = loaded.Selection with { Weight = 0.1234 }
		});
		var window = editor.Window;
		var before = await File.ReadAllBytesAsync(editor.FilePath, TestContext.Current.CancellationToken);

		for (var index = 0; index < 6; index++)
		{
			var page = ShowPage(window, index);
			foreach (var spinner in page.GetLogicalDescendants().OfType<NumericUpDown>())
			{
				spinner.GetVisualDescendants().OfType<TextBox>().First().Focus();
				Dispatcher.UIThread.RunJobs();
			}
		}

		Control(window, "SectionList").Focus();
		Dispatcher.UIThread.RunJobs();

		editor.ViewModel.BuildRecord().Should().Be(editor.Loaded);
		(await editor.ViewModel.SaveCommand.Execute()).Should().BeFalse();
		(await File.ReadAllBytesAsync(editor.FilePath, TestContext.Current.CancellationToken)).Should().Equal(before);
	}

	[AvaloniaFact]
	public async Task Defaults_FillEveryPage_WithTheBuiltInValues()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		ShowPage(window, 1);
		PickColor(window, "ChromeGridLine", Color.Parse("#123456"));

		editor.ViewModel.RestoreDefaultsCommand.Execute().Subscribe();
		for (var index = 0; index < 6; index++)
		{
			ShowPage(window, index);
		}

		var defaults = GridStyleOptions.Default;
		editor.ViewModel.BuildRecord().Should().Be(defaults with
		{
			Chrome = defaults.Chrome with
			{
				Info = editor.Loaded.Chrome.Info,
				PanelBackground = editor.Loaded.Chrome.PanelBackground
			},
			Orientation = editor.Loaded.Orientation
		});
		ShowPage(window, 1);
		Picker(window, "ChromeGridLine").Color
			.Should().Be(defaults.Chrome.GridLine.ToMediaColor(), "the picker still reads the draft after a pick");
	}

	[AvaloniaFact]
	public async Task CtrlClickOnTheSelectedSection_KeepsItsPageShown()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		var list = (ListBox)Control(window, "SectionList");
		var row = list.ContainerFromIndex(0)!;
		var centre = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;

		window.MouseDown(centre, MouseButton.Left, RawInputModifiers.Control);
		window.MouseUp(centre, MouseButton.Left, RawInputModifiers.Control);
		Dispatcher.UIThread.RunJobs();

		list.SelectedIndex.Should().Be(0);
		VisiblePage(window).Name.Should().Be("FontsPage");
	}

	[AvaloniaFact]
	public async Task Escape_ClosesAnOpenDropDownFirst_ThenTheWindow()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		var family = (ComboBox)Control(window, "FontsFontFamily");

		family.Focus();
		family.IsDropDownOpen = true;
		Dispatcher.UIThread.RunJobs();
		window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		family.IsDropDownOpen.Should().BeFalse();
		window.IsVisible.Should().BeTrue("Escape on an open drop-down closes only the drop-down");

		window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		window.IsVisible.Should().BeFalse("Escape with nothing open closes the window");
	}

	[AvaloniaFact]
	public async Task Escape_ClosesAnOpenColourFlyoutFirst_KeepingThePick_ThenTheWindow()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		ShowPage(window, 1);
		var picker = Picker(window, "ChromeGridBackground");
		var flyout = OpenFlyout(window, picker);
		picker.SetCurrentValue(ColorView.ColorProperty, Color.Parse("#123456"));

		window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		flyout.IsOpen.Should().BeFalse();
		window.IsVisible.Should().BeTrue("Escape on an open colour flyout closes only the flyout");
		editor.ViewModel.Chrome.GridBackground.Color.Should().Be(Color.Parse("#123456"));

		window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
		Dispatcher.UIThread.RunJobs();

		window.IsVisible.Should().BeFalse("with the flyout closed, Escape closes the window");
	}

	[AvaloniaFact]
	public async Task Close_WithUnsavedEdits_WritesNothing()
	{
		using var editor = await ShownEditor.OpenAsync();
		var window = editor.Window;
		var before = await File.ReadAllBytesAsync(editor.FilePath, TestContext.Current.CancellationToken);
		ShowPage(window, 1);
		PickColor(window, "ChromeGridBackground", Color.Parse("#123456"));

		((Button)Control(window, "CloseButton")).RaiseEvent(
			new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();

		window.IsVisible.Should().BeFalse();
		(await File.ReadAllBytesAsync(editor.FilePath, TestContext.Current.CancellationToken)).Should().Equal(before);
	}

	private static List<WiringCase> WiringCases()
	{
		return
		[
			new(0, "FontsFontFamily", window => Pick(window, "FontsFontFamily", ""),
				o => o with { Fonts = o.Fonts with { FontFamily = "" } }),
			new(0, "FontsHeaderFontSize", window => SetNumber(window, "FontsHeaderFontSize", 7),
				o => o with { Fonts = o.Fonts with { HeaderFontSize = 7 } }),
			new(0, "FontsHeaderFontWeight", window => Pick(window, "FontsHeaderFontWeight", 300),
				o => o with { Fonts = o.Fonts with { HeaderFontWeight = 300 } }),
			new(0, "FontsHeaderItalic", window => Toggle(window, "FontsHeaderItalic"),
				o => o with { Fonts = o.Fonts with { HeaderItalic = !o.Fonts.HeaderItalic } }),
			new(0, "FontsCellFontSize", window => SetNumber(window, "FontsCellFontSize", 8),
				o => o with { Fonts = o.Fonts with { CellFontSize = 8 } }),
			new(0, "FontsCellFontWeight", window => Pick(window, "FontsCellFontWeight", 600),
				o => o with { Fonts = o.Fonts with { CellFontWeight = 600 } }),
			new(0, "FontsCellItalic", window => Toggle(window, "FontsCellItalic"),
				o => o with { Fonts = o.Fonts with { CellItalic = !o.Fonts.CellItalic } }),
			new(0, "StatusBarFontSize", window => SetNumber(window, "StatusBarFontSize", 9),
				o => o with { StatusBar = o.StatusBar with { FontSize = 9 } }),
			new(0, "StatusBarWeight", window => Pick(window, "StatusBarWeight", 900),
				o => o with { StatusBar = o.StatusBar with { Weight = 900 } }),
			new(0, "StatusBarItalic", window => Toggle(window, "StatusBarItalic"),
				o => o with { StatusBar = o.StatusBar with { Italic = !o.StatusBar.Italic } }),
			new(1, "LayoutRowHeight", window => SetNumber(window, "LayoutRowHeight", 31),
				o => o with { Layout = o.Layout with { RowHeight = 31 } }),
			new(1, "LayoutCellPaddingLeft", window => SetNumber(window, "LayoutCellPaddingLeft", 11),
				o => o with { Layout = o.Layout with { CellPaddingLeft = 11 } }),
			new(1, "LayoutCellPaddingTop", window => SetNumber(window, "LayoutCellPaddingTop", 12),
				o => o with { Layout = o.Layout with { CellPaddingTop = 12 } }),
			new(1, "LayoutCellPaddingRight", window => SetNumber(window, "LayoutCellPaddingRight", 13),
				o => o with { Layout = o.Layout with { CellPaddingRight = 13 } }),
			new(1, "LayoutCellPaddingBottom", window => SetNumber(window, "LayoutCellPaddingBottom", 14),
				o => o with { Layout = o.Layout with { CellPaddingBottom = 14 } }),
			PickCase(1, "ChromeGridBackground", "#0000A1",
				(o, c) => o with { Chrome = o.Chrome with { GridBackground = c } }),
			PickCase(1, "ChromeGridBorder", "#0000A2",
				(o, c) => o with { Chrome = o.Chrome with { GridBorder = c } }),
			PickCase(1, "ChromeGridLine", "#0000A3",
				(o, c) => o with { Chrome = o.Chrome with { GridLine = c } }),
			PickCase(1, "ChromeHeaderForeground", "#0000A4",
				(o, c) => o with { Chrome = o.Chrome with { HeaderForeground = c } }),
			PickCase(2, "ChangedCellsChanged", "#0000A5",
				(o, c) => o with { ChangedCells = o.ChangedCells with { Changed = c } }),
			PickCase(2, "GreyCellsReadOnly", "#0000A6",
				(o, c) => o with { ReadOnlyCell = c }),
			PickCase(2, "GreyCellsDisabled", "#0000A7",
				(o, c) => o with { DisabledCell = c }),
			PickCase(2, "SelectionAccent", "#0000A8",
				(o, c) => o with { Selection = o.Selection with { Accent = c } }),
			new(2, "SelectionWeight", window => SetNumber(window, "SelectionWeight", 21),
				o => o with { Selection = o.Selection with { Weight = 0.21 } }),
			PickCase(3, "ExecutionRunning", "#0000A9",
				(o, c) => o with { Execution = o.Execution with { Running = c } }),
			new(3, "ExecutionRunningWeight", window => SetNumber(window, "ExecutionRunningWeight", 22),
				o => o with { Execution = o.Execution with { RunningWeight = 0.22 } }),
			new(3, "ExecutionPastWeight", window => SetNumber(window, "ExecutionPastWeight", 23),
				o => o with { Execution = o.Execution with { PastWeight = 0.23 } }),
			PickCase(3, "LoopColor", "#0000AA",
				(o, c) => o with { Loop = o.Loop with { Color = c } }),
			new(3, "LoopDepth1Weight", window => SetNumber(window, "LoopDepth1Weight", 24),
				o => o with { Loop = o.Loop with { Depth1Weight = 0.24 } }),
			new(3, "LoopDepth2Weight", window => SetNumber(window, "LoopDepth2Weight", 25),
				o => o with { Loop = o.Loop with { Depth2Weight = 0.25 } }),
			new(3, "LoopDepth3Weight", window => SetNumber(window, "LoopDepth3Weight", 26),
				o => o with { Loop = o.Loop with { Depth3Weight = 0.26 } }),
			new(4, "StatusBarPadding", window => SetNumber(window, "StatusBarPadding", 15),
				o => o with { StatusBar = o.StatusBar with { Padding = 15 } }),
			PickCase(4, "StatusBarBackground", "#0000AB",
				(o, c) => o with { StatusBar = o.StatusBar with { Background = c } }),
			PickCase(4, "StatusBarForeground", "#0000AC",
				(o, c) => o with { StatusBar = o.StatusBar with { Foreground = c } }),
			PickCase(4, "ChromeSecondaryForeground", "#0000AD",
				(o, c) => o with { Chrome = o.Chrome with { SecondaryForeground = c } }),
			PickCase(4, "ChromeConnected", "#0000AE",
				(o, c) => o with { Chrome = o.Chrome with { Connected = c } }),
			PickCase(4, "ChromeDisconnected", "#0000AF",
				(o, c) => o with { Chrome = o.Chrome with { Disconnected = c } }),
			PickCase(4, "ChromeSeparator", "#0000B1",
				(o, c) => o with { Chrome = o.Chrome with { Separator = c } }),
			PickCase(4, "ChromeSubtleBorder", "#0000B2",
				(o, c) => o with { Chrome = o.Chrome with { SubtleBorder = c } }),
			new(5, "ValidationPanelMaxHeight", window => SetNumber(window, "ValidationPanelMaxHeight", 222),
				o => o with { ValidationPanel = o.ValidationPanel with { MaxHeight = 222 } }),
			PickCase(5, "ValidationPanelBackground", "#0000B3",
				(o, c) => o with { ValidationPanel = o.ValidationPanel with { Background = c } }),
			PickCase(5, "ChromePanelHeaderBackground", "#0000B4",
				(o, c) => o with { Chrome = o.Chrome with { PanelHeaderBackground = c } }),
			PickCase(5, "ValidationPanelForeground", "#0000B5",
				(o, c) => o with { ValidationPanel = o.ValidationPanel with { Foreground = c } }),
			PickCase(5, "ValidationPanelErrorColor", "#0000B6",
				(o, c) => o with { ValidationPanel = o.ValidationPanel with { ErrorColor = c } }),
			PickCase(5, "ValidationPanelWarningColor", "#0000B7",
				(o, c) => o with { ValidationPanel = o.ValidationPanel with { WarningColor = c } })
		];
	}

	private static void SetNumber(Window window, string name, decimal value)
	{
		((NumericUpDown)Control(window, name)).Value = value;
	}

	private static WiringCase PickCase(
		int page,
		string name,
		string hex,
		Func<GridStyleOptions, StyleColor, GridStyleOptions> expect)
	{
		var picked = Color.Parse(hex);
		return new WiringCase(
			page,
			name,
			window => PickColor(window, name, picked),
			options => expect(options, picked.ToStyleColor()),
			picked);
	}

	private static void PickColor(Window window, string name, Color color)
	{
		var picker = Picker(window, name);
		var flyout = OpenFlyout(window, picker);
		picker.SetCurrentValue(ColorView.ColorProperty, color);
		flyout.Hide();
		Dispatcher.UIThread.RunJobs();
	}

	private static Flyout OpenFlyout(Window window, ColorPicker picker)
	{
		var dropDown = picker.GetVisualDescendants().OfType<DropDownButton>().Single();
		var centre = dropDown.TranslatePoint(
			new Point(dropDown.Bounds.Width / 2, dropDown.Bounds.Height / 2),
			window)!.Value;

		window.MouseDown(centre, MouseButton.Left);
		window.MouseUp(centre, MouseButton.Left);
		Dispatcher.UIThread.RunJobs();

		var flyout = dropDown.Flyout.Should().BeOfType<Flyout>().Subject;
		flyout.IsOpen.Should().BeTrue("a click on the swatch opens the colour flyout");
		return flyout;
	}

	private static void Toggle(Window window, string name)
	{
		var checkBox = (CheckBox)Control(window, name);
		checkBox.IsChecked = checkBox.IsChecked != true;
	}

	private static void Pick(Window window, string name, object value)
	{
		var comboBox = (ComboBox)Control(window, name);
		comboBox.SelectedItem = comboBox.Items.Single(item => item switch
		{
			FontWeightOption weight => Equals(weight.Value, value),
			FontFamilyOption family => Equals(family.Value, value),
			_ => false
		});
	}

	private static Control Control(Window window, string name)
	{
		return window.FindControl<Control>(name)
			?? throw new InvalidOperationException($"No control named {name}");
	}

	private static Grid ShowPage(Window window, int index)
	{
		((ListBox)Control(window, "SectionList")).SelectedIndex = index;
		Dispatcher.UIThread.RunJobs();
		return VisiblePage(window);
	}

	private static Grid VisiblePage(Window window)
	{
		return Control(window, "Pages").GetVisualDescendants()
			.OfType<Grid>()
			.Single(grid => grid.Classes.Contains("page") && grid.IsEffectivelyVisible);
	}

	private static Rect BoundsIn(Window window, Control control)
	{
		var topLeft = control.TranslatePoint(default, window)
			?? throw new InvalidOperationException($"{control.Name} is not in the window");
		return new Rect(topLeft, control.Bounds.Size);
	}

	private static TextBox TypeInto(Window window, string spinnerName, string text)
	{
		var spinner = (NumericUpDown)Control(window, spinnerName);
		var box = spinner.GetVisualDescendants().OfType<TextBox>().First();
		box.Focus();
		box.Text = text;
		Control(window, "CloseButton").Focus().Should().BeTrue("the spinner commits its text when it loses focus");
		Dispatcher.UIThread.RunJobs();
		return box;
	}

	private static NumberDraft NumberDraftOf(GridStyleEditorViewModel viewModel, string spinnerName)
	{
		return spinnerName switch
		{
			"LayoutRowHeight" => viewModel.Layout.RowHeight,
			"LayoutCellPaddingLeft" => viewModel.Layout.CellPaddingLeft,
			"SelectionWeight" => viewModel.Selection.Weight,
			_ => throw new ArgumentOutOfRangeException(nameof(spinnerName), spinnerName, null)
		};
	}

	private static ColorPicker Picker(Window window, string name)
	{
		return ((ColorFieldView)Control(window, name)).FindControl<ColorPicker>("Picker")
			?? throw new InvalidOperationException($"The colour field {name} has no picker");
	}

	private static Color SwatchColour(ColorPicker picker)
	{
		var swatch = picker.Content.Should().BeOfType<Border>().Subject;
		return ((ISolidColorBrush)swatch.Background!).Color;
	}

	private static PathIcon SectionGlyph(Window window, int index)
	{
		var item = ((ListBox)Control(window, "SectionList")).ContainerFromIndex(index)
			?? throw new InvalidOperationException($"No section row {index}");
		return item.GetVisualDescendants().OfType<PathIcon>().Single();
	}

	private static Color ContentBorderColour(TextBox textBox)
	{
		var border = textBox.GetVisualDescendants()
			.OfType<Border>()
			.Single(candidate => candidate.Name == "PART_ContentPresenterBorder");
		return ((ISolidColorBrush)border.BorderBrush!).Color;
	}

	private static List<T> DraftLeaves<T>(GridStyleEditorViewModel viewModel)
	{
		object[] drafts =
		[
			viewModel.Fonts, viewModel.Layout, viewModel.Selection, viewModel.ChangedCells, viewModel.GreyCells,
			viewModel.Execution, viewModel.Loop, viewModel.StatusBar, viewModel.ValidationPanel, viewModel.Chrome
		];
		return
		[
			.. drafts.SelectMany(draft => draft.GetType().GetProperties()
				.Where(property => property.PropertyType == typeof(T))
				.Select(property => (T)property.GetValue(draft)!))
		];
	}

	private sealed record WiringCase(
		int Page,
		string Control,
		Action<Window> Change,
		Func<GridStyleOptions, GridStyleOptions> Expect,
		Color? Picked = null);

	private sealed class ShownEditor : IDisposable
	{
		private readonly TempDirectory _configDir;
		private readonly GridStyleEditorFacade _facade = new();

		private ShownEditor(TempDirectory configDir, GridStyleOptions loaded)
		{
			_configDir = configDir;
			Loaded = loaded;
			ViewModel = new GridStyleEditorViewModel(
				_facade,
				configDir.Path,
				loaded,
				NullLogger<GridStyleEditorViewModel>.Instance);
			Window = new GridStyleEditorWindow { ViewModel = ViewModel };
			CellPaletteInstaller.Install(Window.Resources, loaded);
			Window.Show();
			Dispatcher.UIThread.RunJobs();
		}

		public GridStyleOptions Loaded { get; }

		public GridStyleEditorViewModel ViewModel { get; }

		public GridStyleEditorWindow Window { get; }

		public string FilePath => Path.Combine(_configDir.Path, "ui", "grid_style.yaml");

		public Color ErrorColour => Loaded.ValidationPanel.ErrorColor.ToMediaColor();

		public static async Task<ShownEditor> OpenAsync(
			Func<GridStyleOptions, GridStyleOptions>? seed = null,
			Func<string, string>? editYaml = null)
		{
			var configDir = CopyShippedConfig("MBE");
			if (editYaml is not null)
			{
				var filePath = Path.Combine(configDir.Path, "ui", "grid_style.yaml");
				await File.WriteAllTextAsync(filePath, editYaml(await File.ReadAllTextAsync(filePath)));
			}

			var loaded = (await new GridStyleEditorFacade().Load(configDir.Path)).Value;
			return new ShownEditor(configDir, seed?.Invoke(loaded) ?? loaded);
		}

		public async Task<GridStyleOptions> Reload()
		{
			return (await _facade.Load(_configDir.Path)).Value;
		}

		public void Dispose()
		{
			Window.Close();
			ViewModel.Dispose();
			_configDir.Dispose();
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
}
