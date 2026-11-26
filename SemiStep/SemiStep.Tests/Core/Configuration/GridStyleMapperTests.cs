using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Mapping;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Component", "Config")]
[Trait("Category", "Unit")]
[Trait("Area", "GridStyleMapping")]
public sealed class GridStyleMapperTests
{
	[Fact]
	public void Map_WiresEveryCellPaletteFieldFromDto()
	{
		var dto = BuildCompleteDto();

		var options = GridStyleMapper.Map(dto).Value;

		options.Selection.Background.Should().Be(StyleColor.Parse("#ABCDEF"));
		options.Selection.Foreground.Should().Be(StyleColor.Parse("#FEDCBA"));
		options.ChangedCells.Changed.Should().Be(StyleColor.Parse("#0ABBCD"));
		options.ChangedCells.ChangedSelected.Should().Be(StyleColor.Parse("#0DCCEF"));

		options.ReadOnlyCells.Depth0.Should().Be(StyleColor.Parse("#11111B"));
		options.ReadOnlyCells.Depth1.Should().Be(StyleColor.Parse("#22222B"));
		options.ReadOnlyCells.Depth2.Should().Be(StyleColor.Parse("#33333B"));
		options.ReadOnlyCells.Depth3.Should().Be(StyleColor.Parse("#44444B"));
		options.ReadOnlyCells.Depth0Past.Should().Be(StyleColor.Parse("#55555B"));
		options.ReadOnlyCells.Depth1Past.Should().Be(StyleColor.Parse("#66666B"));
		options.ReadOnlyCells.Depth2Past.Should().Be(StyleColor.Parse("#77777B"));
		options.ReadOnlyCells.Depth3Past.Should().Be(StyleColor.Parse("#88888B"));
		options.ReadOnlyCells.Selected.Should().Be(StyleColor.Parse("#99999B"));
		options.ReadOnlyCells.Foreground.Should().Be(StyleColor.Parse("#AAAAAC"));

		options.DisabledCells.Depth0.Should().Be(StyleColor.Parse("#11111A"));
		options.DisabledCells.Depth1.Should().Be(StyleColor.Parse("#22222A"));
		options.DisabledCells.Depth2.Should().Be(StyleColor.Parse("#33333A"));
		options.DisabledCells.Depth3.Should().Be(StyleColor.Parse("#44444A"));
		options.DisabledCells.Depth0Past.Should().Be(StyleColor.Parse("#55555A"));
		options.DisabledCells.Depth1Past.Should().Be(StyleColor.Parse("#66666A"));
		options.DisabledCells.Depth2Past.Should().Be(StyleColor.Parse("#77777A"));
		options.DisabledCells.Depth3Past.Should().Be(StyleColor.Parse("#88888A"));
		options.DisabledCells.Selected.Should().Be(StyleColor.Parse("#99999A"));
		options.DisabledCells.Foreground.Should().Be(StyleColor.Parse("#AAAAAB"));

		options.Execution.Depth0.Should().Be(StyleColor.Parse("#111111"));
		options.Execution.Depth1.Should().Be(StyleColor.Parse("#222222"));
		options.Execution.Depth2.Should().Be(StyleColor.Parse("#333333"));
		options.Execution.Depth3.Should().Be(StyleColor.Parse("#444444"));
		options.Execution.Depth0Past.Should().Be(StyleColor.Parse("#555555"));
		options.Execution.Depth1Past.Should().Be(StyleColor.Parse("#666666"));
		options.Execution.Depth2Past.Should().Be(StyleColor.Parse("#777777"));
		options.Execution.Depth3Past.Should().Be(StyleColor.Parse("#888888"));
		options.Execution.CurrentStepMarker.Should().Be(StyleColor.Parse("#999999"));
	}

	[Fact]
	public void Map_WiresEveryChromeFieldFromDto()
	{
		var dto = BuildCompleteDto();
		dto.Chrome = new GridStyleChromeColorsDto
		{
			Info = "#1976D2",
			Connected = "#44BB44",
			Disconnected = "#FF4444",
			PanelBackground = "#F8F8F8",
			PanelHeaderBackground = "#EEEEEE",
			SubtleBorder = "#D0D0D0",
			Separator = "#C0C0C0",
			SecondaryForeground = "#888888",
			GridBorder = "#808080",
			GridBackground = "#FFFFFF",
			HeaderForeground = "#000000"
		};

		var options = GridStyleMapper.Map(dto).Value;

		options.Chrome.Info.Should().Be(StyleColor.Parse("#1976D2"));
		options.Chrome.Connected.Should().Be(StyleColor.Parse("#44BB44"));
		options.Chrome.Disconnected.Should().Be(StyleColor.Parse("#FF4444"));
		options.Chrome.PanelBackground.Should().Be(StyleColor.Parse("#F8F8F8"));
		options.Chrome.PanelHeaderBackground.Should().Be(StyleColor.Parse("#EEEEEE"));
		options.Chrome.SubtleBorder.Should().Be(StyleColor.Parse("#D0D0D0"));
		options.Chrome.Separator.Should().Be(StyleColor.Parse("#C0C0C0"));
		options.Chrome.SecondaryForeground.Should().Be(StyleColor.Parse("#888888"));
		options.Chrome.GridBorder.Should().Be(StyleColor.Parse("#808080"));
		options.Chrome.GridBackground.Should().Be(StyleColor.Parse("#FFFFFF"));
		options.Chrome.HeaderForeground.Should().Be(StyleColor.Parse("#000000"));
	}

	[Fact]
	public void Map_OmittedChromeSection_FallsBackToDefaults()
	{
		var dto = BuildCompleteDto();

		var options = GridStyleMapper.Map(dto).Value;

		options.Chrome.Info.Should().Be(GridStyleOptions.Default.Chrome.Info);
		options.Chrome.GridBorder.Should().Be(GridStyleOptions.Default.Chrome.GridBorder);
		options.Chrome.HeaderForeground.Should().Be(GridStyleOptions.Default.Chrome.HeaderForeground);
	}

	[Fact]
	public void Map_NullDto_FailsWithConfigMissing()
	{
		var result = GridStyleMapper.Map(null);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle().Which.Should().BeOfType<GridStyleConfigMissingError>();
	}

	[Fact]
	public void Map_OmittedChangedColor_FallsBackToDefault()
	{
		var dto = BuildCompleteDto();
		dto.Colors!.Cells!.Changed = null;

		var options = GridStyleMapper.Map(dto).Value;

		options.ChangedCells.Changed.Should().Be(GridStyleOptions.Default.ChangedCells.Changed);
	}

	[Fact]
	public void Map_WiresStatusBarFontFieldsFromDto()
	{
		var dto = BuildCompleteDto();
		dto.StatusBar = new StatusBarStyleDto
		{
			FontSize = 16,
			Weight = 500,
			Italic = true
		};

		var options = GridStyleMapper.Map(dto).Value;

		options.StatusBar.FontSize.Should().Be(16);
		options.StatusBar.Weight.Should().Be(500);
		options.StatusBar.Italic.Should().BeTrue();
	}

	[Fact]
	public void Map_WiresGridFontFieldsFromDto()
	{
		var dto = BuildCompleteDto();
		dto.Fonts = new GridStyleFontsDto
		{
			Family = "Cascadia Mono",
			HeaderSize = 20,
			HeaderWeight = 800,
			HeaderItalic = true,
			CellSize = 15,
			CellWeight = 300,
			CellItalic = true
		};

		var options = GridStyleMapper.Map(dto).Value;

		options.Fonts.FontFamily.Should().Be("Cascadia Mono");
		options.Fonts.HeaderFontSize.Should().Be(20);
		options.Fonts.HeaderFontWeight.Should().Be(800);
		options.Fonts.HeaderItalic.Should().BeTrue();
		options.Fonts.CellFontSize.Should().Be(15);
		options.Fonts.CellFontWeight.Should().Be(300);
		options.Fonts.CellItalic.Should().BeTrue();
	}

	[Fact]
	public void Map_OmittedFontFields_FallBackToDefaults()
	{
		var dto = BuildCompleteDto();

		var options = GridStyleMapper.Map(dto).Value;

		options.Fonts.FontFamily.Should().Be(GridStyleOptions.Default.Fonts.FontFamily);
		options.Fonts.HeaderFontSize.Should().Be(12);
		options.Fonts.HeaderFontWeight.Should().Be(600);
		options.Fonts.HeaderItalic.Should().BeFalse();
		options.Fonts.CellFontSize.Should().Be(12);
		options.Fonts.CellFontWeight.Should().Be(400);
		options.Fonts.CellItalic.Should().BeFalse();
		options.StatusBar.FontSize.Should().Be(12);
		options.StatusBar.Weight.Should().Be(400);
		options.StatusBar.Italic.Should().BeFalse();
	}

	[Fact]
	public void Map_OptionsToDto_WritesEveryFontField()
	{
		var defaults = GridStyleOptions.Default;
		var options = defaults with
		{
			Fonts = defaults.Fonts with
			{
				FontFamily = "Cascadia Mono",
				HeaderFontSize = 20,
				HeaderFontWeight = 800,
				HeaderItalic = true,
				CellFontSize = 15,
				CellFontWeight = 300,
				CellItalic = true
			},
			StatusBar = defaults.StatusBar with
			{
				FontSize = 18,
				Weight = 500,
				Italic = true
			}
		};

		var dto = GridStyleDtoMapper.Map(options);

		dto.Fonts!.Family.Should().Be("Cascadia Mono");
		dto.Fonts!.HeaderSize.Should().Be(20);
		dto.Fonts!.HeaderWeight.Should().Be(800);
		dto.Fonts!.HeaderItalic.Should().BeTrue();
		dto.Fonts!.CellSize.Should().Be(15);
		dto.Fonts!.CellWeight.Should().Be(300);
		dto.Fonts!.CellItalic.Should().BeTrue();
		dto.StatusBar!.FontSize.Should().Be(18);
		dto.StatusBar!.Weight.Should().Be(500);
		dto.StatusBar!.Italic.Should().BeTrue();
	}

	private static GridStyleOptionsDto BuildCompleteDto()
	{
		return new GridStyleOptionsDto
		{
			Colors = new GridStyleColorsDto
			{
				Selection = new GridStyleSelectionColorsDto
				{
					Background = "#ABCDEF",
					Foreground = "#FEDCBA"
				},
				Cells = new GridStyleCellColorsDto
				{
					Changed = "#0ABBCD",
					ChangedSelected = "#0DCCEF",
					ReadOnly = new GridStyleReadOnlyCellColorsDto
					{
						Depth0 = "#11111B",
						Depth1 = "#22222B",
						Depth2 = "#33333B",
						Depth3 = "#44444B",
						Depth0Past = "#55555B",
						Depth1Past = "#66666B",
						Depth2Past = "#77777B",
						Depth3Past = "#88888B",
						Selected = "#99999B",
						Foreground = "#AAAAAC"
					},
					Disabled = new GridStyleDisabledCellColorsDto
					{
						Depth0 = "#11111A",
						Depth1 = "#22222A",
						Depth2 = "#33333A",
						Depth3 = "#44444A",
						Depth0Past = "#55555A",
						Depth1Past = "#66666A",
						Depth2Past = "#77777A",
						Depth3Past = "#88888A",
						Selected = "#99999A",
						Foreground = "#AAAAAB"
					},
					Execution = new GridStyleExecutionColorsDto
					{
						Depth0 = "#111111",
						Depth1 = "#222222",
						Depth2 = "#333333",
						Depth3 = "#444444",
						Depth0Past = "#555555",
						Depth1Past = "#666666",
						Depth2Past = "#777777",
						Depth3Past = "#888888",
						CurrentStepMarker = "#999999"
					}
				}
			}
		};
	}
}
