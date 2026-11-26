using AwesomeAssertions;

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
	public void Validate_NullCells_FailsWithExactlyTwoSectionErrors()
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
			.BeEquivalentTo("colors.cells.readonly", "colors.cells.disabled");
	}

	[Fact]
	public void Validate_MissingDisabledDepth0_FailsWithKeyName()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Disabled!.Depth0 = null;

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle(e => e is GridStyleKeyMissingError
			&& ((GridStyleKeyMissingError)e).Section == "colors.cells.disabled"
			&& ((GridStyleKeyMissingError)e).Key == "depth_0");
	}

	[Fact]
	public void Validate_MissingReadOnlyDepth0_FailsWithKeyName()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.ReadOnly!.Depth0 = null;

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().ContainSingle(e => e is GridStyleKeyMissingError
			&& ((GridStyleKeyMissingError)e).Section == "colors.cells.readonly"
			&& ((GridStyleKeyMissingError)e).Key == "depth_0");
	}

	[Theory]
	[InlineData("#ZZZZZZ")]
	[InlineData("#FFFFFFFFF")]
	[InlineData("FFFFFF")]
	[InlineData("#12345")]
	public void Validate_MalformedHex_FailsNamingKey(string badHex)
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Execution!.Running = badHex;

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'colors.cells.execution.running'") && e.Message.Contains(badHex));
	}

	[Fact]
	public void Validate_EmptyValue_FailsNamingKey()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Execution!.Running = "";

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.execution.running'"));
	}

	[Fact]
	public void Validate_MultipleErrors_AllCollected()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Execution!.Running = "not a color";
		dto.Colors!.Cells!.ReadOnly!.Depth0 = null;
		dto.Colors!.Cells!.Loop = new GridStyleLoopColorsDto { Color = "#GG" };

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.execution.running'"));
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.readonly.depth_0'"));
		result.Errors.Should().Contain(e => e.Message.Contains("'colors.cells.loop.color'"));
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
		dto.Colors!.Cells!.Execution!.Running = hex;

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
			Accent = "#3574F0",
			Weight = 0.25
		};

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_MalformedSelectionHex_FailsNamingKey()
	{
		var dto = CreateValidDto();
		dto.Colors!.Selection = new GridStyleSelectionColorsDto { Accent = "not-a-color" };

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'colors.selection.accent'") && e.Message.Contains("not-a-color"));
	}

	[Fact]
	public void Validate_ValidChangedCellColors_Succeeds()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Changed = "#FFCC80";

		var result = GridStyleMapper.Map(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_MalformedChangedCellHex_FailsNamingKey()
	{
		var dto = CreateValidDto();
		dto.Colors!.Cells!.Changed = "not-a-color";

		var result = GridStyleMapper.Map(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'colors.cells.changed'") && e.Message.Contains("not-a-color"));
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

	private static GridStyleGreyCellColorsDto CreateValidReadOnly()
	{
		return new GridStyleGreyCellColorsDto { Depth0 = "#D8D8D8" };
	}

	private static GridStyleExecutionColorsDto CreateValidExecution()
	{
		return new GridStyleExecutionColorsDto
		{
			Running = "#208A3C",
			RunningWeight = 0.30,
			PastWeight = 0.12
		};
	}

	private static GridStyleGreyCellColorsDto CreateValidDisabled()
	{
		return new GridStyleGreyCellColorsDto { Depth0 = "#E0E0E0" };
	}
}
