using Avalonia.Headless.XUnit;
using Avalonia.Media;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.UI.StyleEditor;

using Xunit;

namespace SemiStep.Tests.UI.StyleEditor;

[Trait("Component", "UI")]
[Trait("Category", "Unit")]
public sealed class GridStyleEditorViewModelTests
{
	private const string ConfigDir = @"C:\does-not-exist";

	[AvaloniaFact]
	public void Seed_PopulatesColorAndNumericProps_FromRecord()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		viewModel.CellFontSize.Should().Be(source.CellFontSize);
		viewModel.RowHeight.Should().Be((decimal)source.RowHeight);
		viewModel.ValidationPanelMaxHeight.Should().Be((decimal)source.ValidationPanelMaxHeight);

		viewModel.SelectionBackground.Should().Be(Color.Parse(source.SelectionBackgroundColor));
		viewModel.HeaderForeground.Should().Be(Color.Parse(source.HeaderForegroundColor));
	}

	[AvaloniaFact]
	public void Seed_RoundTripsShippedHexValues_Losslessly()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		var record = viewModel.BuildRecord();

		record.Should().Be(source);
	}

	[AvaloniaFact]
	public void BuildRecord_AfterEditingColorAndFontSize_ChangesOnlyThoseFields()
	{
		var source = GridStyleOptions.Default;
		var viewModel = CreateViewModel(source);

		viewModel.CellFontSize = 18;
		viewModel.SelectionBackground = Color.Parse("#123456");

		var record = viewModel.BuildRecord();

		var expected = source with
		{
			CellFontSize = 18,
			SelectionBackgroundColor = "#123456"
		};
		record.Should().Be(expected);
	}

	[AvaloniaFact]
	public void CanSave_IsTrue_ForSeededValidDraft()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.CanSave.Should().BeTrue();
	}

	[AvaloniaTheory]
	[InlineData(0)]
	[InlineData(5)]
	[InlineData(73)]
	public void CanSave_IsFalse_WhenFontSizeOutOfRange(int fontSize)
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.CellFontSize = fontSize;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanSave_IsFalse_WhenNumericIsNull()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.RowHeight = null;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void CanSave_IsFalse_WhenRowHeightOutOfRange()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.RowHeight = GridStyleEditorViewModel.MaxRowHeight + 1;

		viewModel.CanSave.Should().BeFalse();
	}

	[AvaloniaFact]
	public void ToHex_PreservesSixDigitForm_ForOpaqueColors()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.SelectionBackground = Color.Parse("#A1B2C3");

		viewModel.BuildRecord().SelectionBackgroundColor.Should().Be("#A1B2C3");
	}

	[AvaloniaFact]
	public void ToHex_EmitsEightDigitForm_ForNonOpaqueColors()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);

		viewModel.SelectionBackground = Color.FromArgb(0x80, 0x11, 0x22, 0x33);

		viewModel.BuildRecord().SelectionBackgroundColor.Should().Be("#80112233");
	}

	[AvaloniaFact]
	public async Task LoadAsync_MissingConfigDir_SetsErrorMessageAndDoesNotReseed()
	{
		var viewModel = CreateViewModel(GridStyleOptions.Default);
		viewModel.CellFontSize = 18;

		await viewModel.LoadAsync();

		viewModel.ErrorMessage.Should().NotBeNullOrWhiteSpace();
		viewModel.CellFontSize.Should().Be(18);
	}

	private static GridStyleEditorViewModel CreateViewModel(GridStyleOptions source)
	{
		return new GridStyleEditorViewModel(new GridStyleEditorFacade(), ConfigDir, source);
	}
}
