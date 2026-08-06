using System.Linq;

using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Mapping;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Config")]
[Trait("Area", "GridStyleValidation")]
public sealed class GridStyleColorsValidationTests
{
	[Fact]
	public void Validate_ValidDto_Succeeds()
	{
		var dto = CreateValidDto();

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_NullDto_Fails()
	{
		var result = GridStyleMapper.Map(null);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("missing"));
	}

	[Fact]
	public void Validate_MissingColors_Fails()
	{
		var dto = new GridStyleOptionsDto();

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("colors"));
	}

	[Fact]
	public void Validate_MissingExecutionSection_Fails()
	{
		var dto = new GridStyleOptionsDto
		{
			Colors = new GridStyleColorsDto
			{
				Cells = new GridStyleCellColorsDto
				{
					ReadOnly = CreateValidReadOnly(),
					Disabled = CreateValidDisabled()
				}
			}
		};

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("colors.cells.execution"));
	}

	[Fact]
	public void Validate_MissingDisabledSection_Fails()
	{
		var dto = new GridStyleOptionsDto
		{
			Colors = new GridStyleColorsDto
			{
				Cells = new GridStyleCellColorsDto
				{
					ReadOnly = CreateValidReadOnly(),
					Execution = CreateValidExecution()
				}
			}
		};

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("colors.cells.disabled"));
	}

	[Fact]
	public void Validate_MissingReadOnlySection_Fails()
	{
		var dto = new GridStyleOptionsDto
		{
			Colors = new GridStyleColorsDto
			{
				Cells = new GridStyleCellColorsDto
				{
					Disabled = CreateValidDisabled(),
					Execution = CreateValidExecution()
				}
			}
		};

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("colors.cells.readonly"));
	}

	[Fact]
	public void Validate_NullCells_FailsWithExactlyThreeSectionErrors()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells = null;

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().OnlyContain(error => error is GridStyleSectionMissingError);
		result.Errors
			.Cast<GridStyleSectionMissingError>()
			.Select(error => error.Section)
			.Should()
			.BeEquivalentTo("colors.cells.execution", "colors.cells.readonly", "colors.cells.disabled");
	}

	public static TheoryData<string> DisabledKeyNames =>
		new()
		{
			"depth_0", "depth_1", "depth_2", "depth_3",
			"depth_0_past", "depth_1_past", "depth_2_past", "depth_3_past",
			"selected", "foreground"
		};

	public static TheoryData<string> ReadOnlyKeyNames =>
		new()
		{
			"depth_0", "depth_1", "depth_2", "depth_3",
			"depth_0_past", "depth_1_past", "depth_2_past", "depth_3_past",
			"selected", "foreground"
		};

	public static TheoryData<string> ExecutionKeyNames =>
		new()
		{
			"depth_0", "depth_1", "depth_2", "depth_3",
			"depth_0_past", "depth_1_past", "depth_2_past", "depth_3_past",
			"current_step_marker"
		};

	[Theory]
	[MemberData(nameof(DisabledKeyNames))]
	public void Validate_MissingDisabledKey_FailsWithKeyName(string keyName)
	{
		var dto = CreateValidDto();
		ClearDisabledKey(dto, keyName);

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains($"'colors.cells.disabled.{keyName}'"));
	}

	[Theory]
	[MemberData(nameof(ReadOnlyKeyNames))]
	public void Validate_MissingReadOnlyKey_FailsWithKeyName(string keyName)
	{
		var dto = CreateValidDto();
		ClearReadOnlyKey(dto, keyName);

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains($"'colors.cells.readonly.{keyName}'"));
	}

	[Theory]
	[MemberData(nameof(ExecutionKeyNames))]
	public void Validate_MissingIndividualKey_FailsWithKeyName(string keyName)
	{
		var dto = CreateValidDto();
		ClearKey(dto, keyName);

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains($"'colors.cells.execution.{keyName}'"));
	}

	[Theory]
	[InlineData("#ZZZZZZ")]
	[InlineData("#FFFFFFFFF")]
	[InlineData("FFFFFF")]
	[InlineData("#12345")]
	public void Validate_MalformedHex_FailsNamingKey(string badHex)
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Execution!.Depth0 = badHex;

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'colors.cells.execution.depth_0'") && e.Message.Contains(badHex));
	}

	[Fact]
	public void Validate_EmptyValue_FailsNamingKey()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Execution!.Depth0 = "";

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.execution.depth_0'"));
	}

	[Fact]
	public void Validate_MultipleErrors_AllCollected()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Execution!.Depth0 = "not a color";
		dto.Colors!.Cells!.Execution!.Depth1 = null;
		dto.Colors!.Cells!.Execution!.CurrentStepMarker = "#GG";

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.execution.depth_0'"));
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.execution.depth_1'"));
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.execution.current_step_marker'"));
	}

	[Theory]
	[InlineData("#FFFFFF")]
	[InlineData("#000000")]
	[InlineData("#aabbcc")]
	[InlineData("#FF8800")]
	[InlineData("#AABBCCDD")]
	public void Validate_ValidHexFormats_Succeeds(string hex)
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Execution!.Depth0 = hex;

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_ValidChromeSection_Succeeds()
	{
		var dto = CreateValidDto();
		dto.Chrome = new GridStyleChromeColorsDto
		{
			Info = "#1976D2",
			GridBorder = "#808080",
			HeaderForeground = "#000000"
		};

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_MalformedChromeHex_FailsNamingKey()
	{
		var dto = CreateValidDto();
		dto.Chrome = new GridStyleChromeColorsDto
		{
			GridBorder = "not-a-color"
		};

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'chrome.grid_border'") && e.Message.Contains("not-a-color"));
	}

	[Fact]
	public void Validate_ValidSelectionSection_Succeeds()
	{
		var dto = CreateValidDto();
		dto.Colors!.Selection = new GridStyleSelectionColorsDto
		{
			Background = "#CCE4F7",
			Foreground = "#202020"
		};

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Theory]
	[InlineData("background")]
	[InlineData("foreground")]
	public void Validate_MalformedSelectionHex_FailsNamingKey(string keyName)
	{
		var dto = CreateValidDto();
		dto.Colors!.Selection = new GridStyleSelectionColorsDto
		{
			Background = keyName == "background" ? "not-a-color" : "#CCE4F7",
			Foreground = keyName == "foreground" ? "not-a-color" : "#202020"
		};

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains($"'colors.selection.{keyName}'") && e.Message.Contains("not-a-color"));
	}

	[Fact]
	public void Validate_ValidChangedCellColors_Succeeds()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Changed = "#FFCC80";
		dto.Colors!.Cells!.ChangedSelected = "#EFD3A4";

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Theory]
	[InlineData("changed")]
	[InlineData("changed_selected")]
	public void Validate_MalformedChangedCellHex_FailsNamingKey(string keyName)
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Changed = keyName == "changed" ? "not-a-color" : "#FFCC80";
		dto.Colors!.Cells!.ChangedSelected = keyName == "changed_selected" ? "not-a-color" : "#EFD3A4";

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains($"'colors.cells.{keyName}'") && e.Message.Contains("not-a-color"));
	}

	[Fact]
	public void Validate_ValidGridLine_Succeeds()
	{
		var dto = CreateValidDto();
		dto.Colors!.GridLine = "#CCCCCC";

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_MalformedGridLineHex_FailsNamingKey()
	{
		var dto = CreateValidDto();
		dto.Colors!.GridLine = "not-a-color";

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'colors.grid_line'") && e.Message.Contains("not-a-color"));
	}

	[Fact]
	public void Validate_ValidStatusBarSection_Succeeds()
	{
		var dto = CreateValidDto();
		dto.StatusBar = new StatusBarStyleDto
		{
			Background = "#F0F0F0",
			Foreground = "#111111"
		};

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Theory]
	[InlineData("background")]
	[InlineData("foreground")]
	public void Validate_MalformedStatusBarHex_FailsNamingKey(string keyName)
	{
		var dto = CreateValidDto();
		dto.StatusBar = new StatusBarStyleDto
		{
			Background = keyName == "background" ? "not-a-color" : "#F0F0F0",
			Foreground = keyName == "foreground" ? "not-a-color" : "#111111"
		};

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains($"'status_bar.{keyName}'") && e.Message.Contains("not-a-color"));
	}

	[Fact]
	public void Validate_ValidValidationPanelSection_Succeeds()
	{
		var dto = CreateValidDto();
		dto.ValidationPanel = new ValidationPanelStyleDto
		{
			Background = "#FBFBFB",
			Foreground = "#222222",
			ErrorColor = "#D32F2F",
			WarningColor = "#F57C00"
		};

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Theory]
	[InlineData("background")]
	[InlineData("foreground")]
	[InlineData("error_color")]
	[InlineData("warning_color")]
	public void Validate_MalformedValidationPanelHex_FailsNamingKey(string keyName)
	{
		var dto = CreateValidDto();
		dto.ValidationPanel = new ValidationPanelStyleDto
		{
			Background = keyName == "background" ? "not-a-color" : "#FBFBFB",
			Foreground = keyName == "foreground" ? "not-a-color" : "#222222",
			ErrorColor = keyName == "error_color" ? "not-a-color" : "#D32F2F",
			WarningColor = keyName == "warning_color" ? "not-a-color" : "#F57C00"
		};

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains($"'validation_panel.{keyName}'") && e.Message.Contains("not-a-color"));
	}

	private static GridStyleOptionsDto CreateValidDto()
	{
		return new GridStyleOptionsDto
		{
			Colors = new GridStyleColorsDto
			{
				Cells = new GridStyleCellColorsDto
				{
					ReadOnly = CreateValidReadOnly(),
					Disabled = CreateValidDisabled(),
					Execution = CreateValidExecution()
				}
			}
		};
	}

	private static GridStyleReadOnlyCellColorsDto CreateValidReadOnly()
	{
		return new GridStyleReadOnlyCellColorsDto
		{
			Depth0 = "#D8D8D8",
			Depth1 = "#CCD5E0",
			Depth2 = "#B8C3D1",
			Depth3 = "#94A2B3",
			Depth0Past = "#C8C8C8",
			Depth1Past = "#BCC4CE",
			Depth2Past = "#ACB7C2",
			Depth3Past = "#8590A0",
			Selected = "#6B95C0",
			Foreground = "#606060"
		};
	}

	private static GridStyleExecutionColorsDto CreateValidExecution()
	{
		return new GridStyleExecutionColorsDto
		{
			Depth0 = "#FFFFFF",
			Depth1 = "#E8F3FF",
			Depth2 = "#D0E7FF",
			Depth3 = "#A8D0FF",
			Depth0Past = "#F0F0F0",
			Depth1Past = "#DCE5EE",
			Depth2Past = "#C4D2E0",
			Depth3Past = "#9CB4CC",
			CurrentStepMarker = "#FF8800"
		};
	}

	private static GridStyleDisabledCellColorsDto CreateValidDisabled()
	{
		return new GridStyleDisabledCellColorsDto
		{
			Depth0 = "#E0E0E0",
			Depth1 = "#D5DEEA",
			Depth2 = "#C2CEDB",
			Depth3 = "#9DABBC",
			Depth0Past = "#D0D0D0",
			Depth1Past = "#C5CDD8",
			Depth2Past = "#B5C0CC",
			Depth3Past = "#909AAA",
			Selected = "#89B4D7",
			Foreground = "#808080"
		};
	}

	private static void ClearDisabledKey(GridStyleOptionsDto dto, string keyName)
	{
		var disabled = dto.Colors!.Cells!.Disabled!;
		switch (keyName)
		{
			case "depth_0":
				disabled.Depth0 = null;
				break;
			case "depth_1":
				disabled.Depth1 = null;
				break;
			case "depth_2":
				disabled.Depth2 = null;
				break;
			case "depth_3":
				disabled.Depth3 = null;
				break;
			case "depth_0_past":
				disabled.Depth0Past = null;
				break;
			case "depth_1_past":
				disabled.Depth1Past = null;
				break;
			case "depth_2_past":
				disabled.Depth2Past = null;
				break;
			case "depth_3_past":
				disabled.Depth3Past = null;
				break;
			case "selected":
				disabled.Selected = null;
				break;
			case "foreground":
				disabled.Foreground = null;
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(keyName), keyName, "Unknown key");
		}
	}

	private static void ClearReadOnlyKey(GridStyleOptionsDto dto, string keyName)
	{
		var readonlyCells = dto.Colors!.Cells!.ReadOnly!;
		switch (keyName)
		{
			case "depth_0":
				readonlyCells.Depth0 = null;
				break;
			case "depth_1":
				readonlyCells.Depth1 = null;
				break;
			case "depth_2":
				readonlyCells.Depth2 = null;
				break;
			case "depth_3":
				readonlyCells.Depth3 = null;
				break;
			case "depth_0_past":
				readonlyCells.Depth0Past = null;
				break;
			case "depth_1_past":
				readonlyCells.Depth1Past = null;
				break;
			case "depth_2_past":
				readonlyCells.Depth2Past = null;
				break;
			case "depth_3_past":
				readonlyCells.Depth3Past = null;
				break;
			case "selected":
				readonlyCells.Selected = null;
				break;
			case "foreground":
				readonlyCells.Foreground = null;
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(keyName), keyName, "Unknown key");
		}
	}

	private static void ClearKey(GridStyleOptionsDto dto, string keyName)
	{
		var exec = dto.Colors!.Cells!.Execution!;
		switch (keyName)
		{
			case "depth_0":
				exec.Depth0 = null;
				break;
			case "depth_1":
				exec.Depth1 = null;
				break;
			case "depth_2":
				exec.Depth2 = null;
				break;
			case "depth_3":
				exec.Depth3 = null;
				break;
			case "depth_0_past":
				exec.Depth0Past = null;
				break;
			case "depth_1_past":
				exec.Depth1Past = null;
				break;
			case "depth_2_past":
				exec.Depth2Past = null;
				break;
			case "depth_3_past":
				exec.Depth3Past = null;
				break;
			case "current_step_marker":
				exec.CurrentStepMarker = null;
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(keyName), keyName, "Unknown key");
		}
	}
}
