using FluentAssertions;

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

		var options = GridStyleMapper.Map(dto);

		options.Selection.Background.Should().Be("#ABCDEF");
		options.Selection.Foreground.Should().Be("#FEDCBA");
		options.ChangedCells.Changed.Should().Be("#0ABBCD");
		options.ChangedCells.ChangedSelected.Should().Be("#0DCCEF");

		options.ReadOnlyCells.Depth0.Should().Be("#11111B");
		options.ReadOnlyCells.Depth1.Should().Be("#22222B");
		options.ReadOnlyCells.Depth2.Should().Be("#33333B");
		options.ReadOnlyCells.Depth3.Should().Be("#44444B");
		options.ReadOnlyCells.Depth0Past.Should().Be("#55555B");
		options.ReadOnlyCells.Depth1Past.Should().Be("#66666B");
		options.ReadOnlyCells.Depth2Past.Should().Be("#77777B");
		options.ReadOnlyCells.Depth3Past.Should().Be("#88888B");
		options.ReadOnlyCells.Selected.Should().Be("#99999B");
		options.ReadOnlyCells.Foreground.Should().Be("#AAAAAC");

		options.DisabledCells.Depth0.Should().Be("#11111A");
		options.DisabledCells.Depth1.Should().Be("#22222A");
		options.DisabledCells.Depth2.Should().Be("#33333A");
		options.DisabledCells.Depth3.Should().Be("#44444A");
		options.DisabledCells.Depth0Past.Should().Be("#55555A");
		options.DisabledCells.Depth1Past.Should().Be("#66666A");
		options.DisabledCells.Depth2Past.Should().Be("#77777A");
		options.DisabledCells.Depth3Past.Should().Be("#88888A");
		options.DisabledCells.Selected.Should().Be("#99999A");
		options.DisabledCells.Foreground.Should().Be("#AAAAAB");

		options.Execution.Depth0.Should().Be("#111111");
		options.Execution.Depth1.Should().Be("#222222");
		options.Execution.Depth2.Should().Be("#333333");
		options.Execution.Depth3.Should().Be("#444444");
		options.Execution.Depth0Past.Should().Be("#555555");
		options.Execution.Depth1Past.Should().Be("#666666");
		options.Execution.Depth2Past.Should().Be("#777777");
		options.Execution.Depth3Past.Should().Be("#888888");
		options.Execution.CurrentStepMarker.Should().Be("#999999");
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
			LocalMode = "#6C707E",
			Connecting = "#FFAF0F",
			PanelBackground = "#F8F8F8",
			PanelHeaderBackground = "#EEEEEE",
			SubtleBorder = "#D0D0D0",
			Separator = "#C0C0C0",
			SecondaryForeground = "#888888",
			GridBorder = "#808080",
			GridBackground = "#FFFFFF",
			HeaderForeground = "#000000"
		};

		var options = GridStyleMapper.Map(dto);

		options.Chrome.Info.Should().Be("#1976D2");
		options.Chrome.Connected.Should().Be("#44BB44");
		options.Chrome.Disconnected.Should().Be("#FF4444");
		options.Chrome.LocalMode.Should().Be("#6C707E");
		options.Chrome.Connecting.Should().Be("#FFAF0F");
		options.Chrome.PanelBackground.Should().Be("#F8F8F8");
		options.Chrome.PanelHeaderBackground.Should().Be("#EEEEEE");
		options.Chrome.SubtleBorder.Should().Be("#D0D0D0");
		options.Chrome.Separator.Should().Be("#C0C0C0");
		options.Chrome.SecondaryForeground.Should().Be("#888888");
		options.Chrome.GridBorder.Should().Be("#808080");
		options.Chrome.GridBackground.Should().Be("#FFFFFF");
		options.Chrome.HeaderForeground.Should().Be("#000000");
	}

	[Fact]
	public void Map_OmittedChromeSection_FallsBackToDefaults()
	{
		var dto = BuildCompleteDto();

		var options = GridStyleMapper.Map(dto);

		options.Chrome.Info.Should().Be(GridStyleOptions.Default.Chrome.Info);
		options.Chrome.LocalMode.Should().Be(GridStyleOptions.Default.Chrome.LocalMode);
		options.Chrome.Connecting.Should().Be(GridStyleOptions.Default.Chrome.Connecting);
		options.Chrome.GridBorder.Should().Be(GridStyleOptions.Default.Chrome.GridBorder);
		options.Chrome.HeaderForeground.Should().Be(GridStyleOptions.Default.Chrome.HeaderForeground);
	}

	[Fact]
	public void Map_NullDto_ReturnsDefaults()
	{
		var options = GridStyleMapper.Map(null);

		options.Should().Be(GridStyleOptions.Default);
	}

	[Fact]
	public void Map_OmittedChangedColor_FallsBackToDefault()
	{
		var dto = BuildCompleteDto();
		dto.Colors!.Cells!.Changed = null;

		var options = GridStyleMapper.Map(dto);

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
			Italic = true,
			TimerLabelFontSize = 18,
			TimerLabelWeight = 600,
			TimerLabelItalic = true,
			TimerValueFontSize = 32,
			TimerValueWeight = 800,
			TimerValueItalic = true
		};

		var options = GridStyleMapper.Map(dto);

		options.StatusBar.FontSize.Should().Be(16);
		options.StatusBar.Weight.Should().Be(500);
		options.StatusBar.Italic.Should().BeTrue();
		options.StatusBar.TimerLabelFontSize.Should().Be(18);
		options.StatusBar.TimerLabelWeight.Should().Be(600);
		options.StatusBar.TimerLabelItalic.Should().BeTrue();
		options.StatusBar.TimerValueFontSize.Should().Be(32);
		options.StatusBar.TimerValueWeight.Should().Be(800);
		options.StatusBar.TimerValueItalic.Should().BeTrue();
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

		var options = GridStyleMapper.Map(dto);

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

		var options = GridStyleMapper.Map(dto);

		options.Fonts.FontFamily.Should().Be(GridStyleOptions.Default.Fonts.FontFamily);
		options.Fonts.HeaderFontSize.Should().Be(14);
		options.Fonts.HeaderFontWeight.Should().Be(700);
		options.Fonts.HeaderItalic.Should().BeFalse();
		options.Fonts.CellFontSize.Should().Be(12);
		options.Fonts.CellFontWeight.Should().Be(400);
		options.Fonts.CellItalic.Should().BeFalse();
		options.StatusBar.FontSize.Should().Be(12);
		options.StatusBar.Weight.Should().Be(400);
		options.StatusBar.Italic.Should().BeFalse();
		options.StatusBar.TimerLabelFontSize.Should().Be(14);
		options.StatusBar.TimerLabelWeight.Should().Be(400);
		options.StatusBar.TimerLabelItalic.Should().BeFalse();
		options.StatusBar.TimerValueFontSize.Should().Be(24);
		options.StatusBar.TimerValueWeight.Should().Be(400);
		options.StatusBar.TimerValueItalic.Should().BeFalse();
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
				Italic = true,
				TimerLabelFontSize = 19,
				TimerLabelWeight = 600,
				TimerLabelItalic = true,
				TimerValueFontSize = 30,
				TimerValueWeight = 800,
				TimerValueItalic = true
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
		dto.StatusBar!.TimerLabelFontSize.Should().Be(19);
		dto.StatusBar!.TimerLabelWeight.Should().Be(600);
		dto.StatusBar!.TimerLabelItalic.Should().BeTrue();
		dto.StatusBar!.TimerValueFontSize.Should().Be(30);
		dto.StatusBar!.TimerValueWeight.Should().Be(800);
		dto.StatusBar!.TimerValueItalic.Should().BeTrue();
	}

	[Fact]
	public void Map_OptionsToDto_WritesLocalModeChromeColor()
	{
		var defaults = GridStyleOptions.Default;
		var options = defaults with { Chrome = defaults.Chrome with { LocalMode = "#123456" } };

		var dto = GridStyleDtoMapper.Map(options);

		dto.Chrome!.LocalMode.Should().Be("#123456");
	}

	[Fact]
	public void Map_OptionsToDto_WritesConnectingChromeColor()
	{
		var defaults = GridStyleOptions.Default;
		var options = defaults with { Chrome = defaults.Chrome with { Connecting = "#123456" } };

		var dto = GridStyleDtoMapper.Map(options);

		dto.Chrome!.Connecting.Should().Be("#123456");
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
