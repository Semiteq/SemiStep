using AwesomeAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Tests.Helpers;

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

		options.Selection.Should().Be(new SelectionColors(StyleColor.Parse("#ABCDEF"), 0.5));
		options.ChangedCells.Changed.Should().Be(StyleColor.Parse("#0ABBCD"));
		options.ReadOnlyCell.Should().Be(StyleColor.Parse("#11111B"));
		options.DisabledCell.Should().Be(StyleColor.Parse("#11111A"));
		options.Execution.Should().Be(new ExecutionPalette(StyleColor.Parse("#111111"), 0.75, 0.25));
		options.Loop.Should().Be(new LoopPalette(StyleColor.Parse("#222222"), 0.125, 0.375, 0.625));
	}

	[Fact]
	public void Map_FileWithOnlyTheOldPaletteKeys_LoadsTheNewKeysAtDefaults()
	{
		const string OldPaletteYaml = """
			colors:
			  selection:
			    background: "#CCE4F7"
			    foreground: "#202020"
			  cells:
			    changed: "#FFCC80"
			    changed_selected: "#EFD3A4"
			    readonly:
			      depth_0: "#D8D8D8"
			      depth_1: "#CCD5E0"
			      selected: "#DCE8F4"
			      foreground: "#606060"
			    disabled:
			      depth_0: "#E0E0E0"
			      depth_1_past: "#C5CDD8"
			    execution:
			      depth_0: "#FFFFFF"
			      depth_1: "#E8F3FF"
			      current_step_marker: "#FF8800"
			""";
		var dto = YamlConfigDeserializer.Instance.Deserialize<GridStyleOptionsDto?>(OldPaletteYaml);

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
		var defaults = GridStyleOptions.Default;
		result.Value.Selection.Should().Be(defaults.Selection);
		result.Value.Execution.Should().Be(defaults.Execution);
		result.Value.Loop.Should().Be(defaults.Loop);
		result.Value.ReadOnlyCell.Should().Be(StyleColor.Parse("#D8D8D8"));
	}

	[Fact]
	public void Map_WithoutTheExecutionSection_Loads()
	{
		var dto = BuildCompleteDto();
		dto.Colors!.Cells!.Execution = null;
		dto.Colors!.Cells!.Loop = null;

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
		result.Value.Execution.Should().Be(GridStyleOptions.Default.Execution);
		result.Value.Loop.Should().Be(GridStyleOptions.Default.Loop);
	}

	[Theory]
	[InlineData(1.5)]
	[InlineData(-0.1)]
	public void Map_RunningWeightOutsideZeroToOne_FailsNamingSectionAndKey(double weight)
	{
		var dto = BuildCompleteDto();
		dto.Colors!.Cells!.Execution!.RunningWeight = weight;

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		var error = result.Errors.Should().ContainSingle().Which.Should().BeOfType<GridStyleWeightOutOfRangeError>().Subject;
		error.Section.Should().Be("colors.cells.execution");
		error.Key.Should().Be("running_weight");
		error.Value.Should().Be(weight);
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(1.0)]
	public void Map_WeightsAtTheEndsOfTheRange_AreAccepted(double weight)
	{
		var dto = BuildCompleteDto();
		var execution = dto.Colors!.Cells!.Execution!;
		var loop = dto.Colors!.Cells!.Loop!;
		execution.RunningWeight = execution.PastWeight = weight;
		loop.Depth1Weight = loop.Depth2Weight = loop.Depth3Weight = weight;
		dto.Colors!.Selection!.Weight = weight;

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
		result.Value.Execution.PastWeight.Should().Be(weight);
		result.Value.Loop.Depth3Weight.Should().Be(weight);
		result.Value.Selection.Weight.Should().Be(weight);
	}

	[Theory]
	[InlineData("past_weight", 1.01)]
	[InlineData("past_weight", -0.01)]
	[InlineData("depth_1_weight", 1.01)]
	[InlineData("depth_1_weight", -0.01)]
	[InlineData("depth_3_weight", 1.01)]
	[InlineData("depth_3_weight", -0.01)]
	public void Map_WeightOutsideZeroToOne_FailsNamingTheKey(string key, double weight)
	{
		var dto = BuildCompleteDto();
		var execution = dto.Colors!.Cells!.Execution!;
		var loop = dto.Colors!.Cells!.Loop!;
		switch (key)
		{
			case "past_weight":
				execution.PastWeight = weight;
				break;
			case "depth_1_weight":
				loop.Depth1Weight = weight;
				break;
			default:
				loop.Depth3Weight = weight;
				break;
		}

		var result = GridStyleMapper.Map(dto);

		var error = result.Errors.Should().ContainSingle().Which.Should().BeOfType<GridStyleWeightOutOfRangeError>().Subject;
		error.Key.Should().Be(key);
		error.Value.Should().Be(weight);
	}

	[Fact]
	public void Map_LoopAndSelectionWeightsOutOfRange_AreCollectedTogether()
	{
		var dto = BuildCompleteDto();
		dto.Colors!.Cells!.Loop!.Depth2Weight = 2;
		dto.Colors!.Selection!.Weight = -1;

		var result = GridStyleMapper.Map(dto);

		result.Errors.OfType<GridStyleWeightOutOfRangeError>()
			.Select(error => $"{error.Section}.{error.Key}")
			.Should()
			.Equal("colors.cells.loop.depth_2_weight", "colors.selection.weight");
	}

	[Fact]
	public void Map_SavedOptionsLoadBackUnchanged()
	{
		var options = GridStyleOptionsTestData.Distinct();

		var reloaded = GridStyleMapper.Map(GridStyleDtoMapper.Map(options));

		reloaded.IsSuccess.Should().BeTrue();
		reloaded.Value.Should().Be(options);
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
					Accent = "#ABCDEF",
					Weight = 0.5
				},
				Cells = new GridStyleCellColorsDto
				{
					Changed = "#0ABBCD",
					ReadOnly = new GridStyleGreyCellColorsDto { Depth0 = "#11111B" },
					Disabled = new GridStyleGreyCellColorsDto { Depth0 = "#11111A" },
					Execution = new GridStyleExecutionColorsDto
					{
						Running = "#111111",
						RunningWeight = 0.75,
						PastWeight = 0.25
					},
					Loop = new GridStyleLoopColorsDto
					{
						Color = "#222222",
						Depth1Weight = 0.125,
						Depth2Weight = 0.375,
						Depth3Weight = 0.625
					}
				}
			}
		};
	}
}
