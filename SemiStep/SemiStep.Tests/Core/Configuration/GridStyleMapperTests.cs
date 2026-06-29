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

		options.SelectionBackgroundColor.Should().Be("#ABCDEF");
		options.SelectionForegroundColor.Should().Be("#FEDCBA");
		options.CellChangedColor.Should().Be("#0ABBCD");
		options.CellChangedSelectedColor.Should().Be("#0DCCEF");

		options.ReadOnlyCellDepth0Color.Should().Be("#11111B");
		options.ReadOnlyCellDepth1Color.Should().Be("#22222B");
		options.ReadOnlyCellDepth2Color.Should().Be("#33333B");
		options.ReadOnlyCellDepth3Color.Should().Be("#44444B");
		options.ReadOnlyCellDepth0PastColor.Should().Be("#55555B");
		options.ReadOnlyCellDepth1PastColor.Should().Be("#66666B");
		options.ReadOnlyCellDepth2PastColor.Should().Be("#77777B");
		options.ReadOnlyCellDepth3PastColor.Should().Be("#88888B");
		options.ReadOnlyCellSelectedColor.Should().Be("#99999B");
		options.ReadOnlyCellForegroundColor.Should().Be("#AAAAAC");

		options.DisabledCellDepth0Color.Should().Be("#11111A");
		options.DisabledCellDepth1Color.Should().Be("#22222A");
		options.DisabledCellDepth2Color.Should().Be("#33333A");
		options.DisabledCellDepth3Color.Should().Be("#44444A");
		options.DisabledCellDepth0PastColor.Should().Be("#55555A");
		options.DisabledCellDepth1PastColor.Should().Be("#66666A");
		options.DisabledCellDepth2PastColor.Should().Be("#77777A");
		options.DisabledCellDepth3PastColor.Should().Be("#88888A");
		options.DisabledCellSelectedColor.Should().Be("#99999A");
		options.DisabledCellForegroundColor.Should().Be("#AAAAAB");

		options.ExecutionDepth0Color.Should().Be("#111111");
		options.ExecutionDepth1Color.Should().Be("#222222");
		options.ExecutionDepth2Color.Should().Be("#333333");
		options.ExecutionDepth3Color.Should().Be("#444444");
		options.ExecutionDepth0PastColor.Should().Be("#555555");
		options.ExecutionDepth1PastColor.Should().Be("#666666");
		options.ExecutionDepth2PastColor.Should().Be("#777777");
		options.ExecutionDepth3PastColor.Should().Be("#888888");
		options.ExecutionCurrentStepMarkerColor.Should().Be("#999999");
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

		var options = GridStyleMapper.Map(dto);

		options.InfoColor.Should().Be("#1976D2");
		options.ConnectedColor.Should().Be("#44BB44");
		options.DisconnectedColor.Should().Be("#FF4444");
		options.PanelBackgroundColor.Should().Be("#F8F8F8");
		options.PanelHeaderBackgroundColor.Should().Be("#EEEEEE");
		options.SubtleBorderColor.Should().Be("#D0D0D0");
		options.SeparatorColor.Should().Be("#C0C0C0");
		options.SecondaryForegroundColor.Should().Be("#888888");
		options.GridBorderColor.Should().Be("#808080");
		options.GridBackgroundColor.Should().Be("#FFFFFF");
		options.HeaderForegroundColor.Should().Be("#000000");
	}

	[Fact]
	public void Map_OmittedChromeSection_FallsBackToDefaults()
	{
		var dto = BuildCompleteDto();

		var options = GridStyleMapper.Map(dto);

		options.InfoColor.Should().Be(GridStyleOptions.Default.InfoColor);
		options.GridBorderColor.Should().Be(GridStyleOptions.Default.GridBorderColor);
		options.HeaderForegroundColor.Should().Be(GridStyleOptions.Default.HeaderForegroundColor);
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

		options.CellChangedColor.Should().Be(GridStyleOptions.Default.CellChangedColor);
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

		options.StatusBarFontSize.Should().Be(16);
		options.StatusBarFontWeight.Should().Be(500);
		options.StatusBarItalic.Should().BeTrue();
		options.StatusBarTimerLabelFontSize.Should().Be(18);
		options.StatusBarTimerLabelFontWeight.Should().Be(600);
		options.StatusBarTimerLabelItalic.Should().BeTrue();
		options.StatusBarTimerValueFontSize.Should().Be(32);
		options.StatusBarTimerValueFontWeight.Should().Be(800);
		options.StatusBarTimerValueItalic.Should().BeTrue();
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

		options.FontFamily.Should().Be("Cascadia Mono");
		options.HeaderFontSize.Should().Be(20);
		options.HeaderFontWeight.Should().Be(800);
		options.HeaderItalic.Should().BeTrue();
		options.CellFontSize.Should().Be(15);
		options.CellFontWeight.Should().Be(300);
		options.CellItalic.Should().BeTrue();
	}

	[Fact]
	public void Map_OmittedFontFields_FallBackToDefaults()
	{
		var dto = BuildCompleteDto();

		var options = GridStyleMapper.Map(dto);

		options.FontFamily.Should().Be(GridStyleOptions.Default.FontFamily);
		options.HeaderFontSize.Should().Be(14);
		options.HeaderFontWeight.Should().Be(700);
		options.HeaderItalic.Should().BeFalse();
		options.CellFontSize.Should().Be(12);
		options.CellFontWeight.Should().Be(400);
		options.CellItalic.Should().BeFalse();
		options.StatusBarFontSize.Should().Be(12);
		options.StatusBarFontWeight.Should().Be(400);
		options.StatusBarItalic.Should().BeFalse();
		options.StatusBarTimerLabelFontSize.Should().Be(14);
		options.StatusBarTimerLabelFontWeight.Should().Be(400);
		options.StatusBarTimerLabelItalic.Should().BeFalse();
		options.StatusBarTimerValueFontSize.Should().Be(24);
		options.StatusBarTimerValueFontWeight.Should().Be(400);
		options.StatusBarTimerValueItalic.Should().BeFalse();
	}

	[Fact]
	public void Map_OptionsToDto_WritesEveryFontField()
	{
		var options = GridStyleOptions.Default with
		{
			FontFamily = "Cascadia Mono",
			HeaderFontSize = 20,
			HeaderFontWeight = 800,
			HeaderItalic = true,
			CellFontSize = 15,
			CellFontWeight = 300,
			CellItalic = true,
			StatusBarFontSize = 18,
			StatusBarFontWeight = 500,
			StatusBarItalic = true,
			StatusBarTimerLabelFontSize = 19,
			StatusBarTimerLabelFontWeight = 600,
			StatusBarTimerLabelItalic = true,
			StatusBarTimerValueFontSize = 30,
			StatusBarTimerValueFontWeight = 800,
			StatusBarTimerValueItalic = true
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
