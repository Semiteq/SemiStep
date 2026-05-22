using FluentAssertions;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Validation;

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

		var result = GridStyleValidator.Validate(dto);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public void Validate_NullDto_Fails()
	{
		var result = GridStyleValidator.Validate(null);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("missing"));
	}

	[Fact]
	public void Validate_MissingColors_Fails()
	{
		var dto = new GridStyleOptionsDto();

		var result = GridStyleValidator.Validate(dto);

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
					Disabled = CreateValidDisabled()
				}
			}
		};

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("colors.execution"));
	}

	[Fact]
	public void Validate_MissingDisabledSection_Fails()
	{
		var dto = new GridStyleOptionsDto
		{
			Colors = new GridStyleColorsDto
			{
				Execution = CreateValidExecution()
			}
		};

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("colors.cells.disabled"));
	}

	[Theory]
	[InlineData("normal")]
	[InlineData("selected")]
	[InlineData("foreground")]
	public void Validate_MissingDisabledKey_FailsWithKeyName(string keyName)
	{
		var dto = CreateValidDto();
		ClearDisabledKey(dto, keyName);

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains($"colors.cells.disabled.{keyName}"));
	}

	[Theory]
	[InlineData("normal", "#ZZZZZZ")]
	[InlineData("selected", "FFFFFF")]
	[InlineData("foreground", "#12345")]
	public void Validate_MalformedDisabledHex_FailsNamingKey(string keyName, string badHex)
	{
		var dto = CreateValidDto();
		SetDisabledKey(dto, keyName, badHex);

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains($"colors.cells.disabled.{keyName}") &&
			e.Message.Contains(badHex));
	}

	[Theory]
	[InlineData("depth_0")]
	[InlineData("depth_1")]
	[InlineData("depth_2")]
	[InlineData("depth_3")]
	[InlineData("depth_0_past")]
	[InlineData("depth_1_past")]
	[InlineData("depth_2_past")]
	[InlineData("depth_3_past")]
	[InlineData("current_step_marker")]
	public void Validate_MissingIndividualKey_FailsWithKeyName(string keyName)
	{
		var dto = CreateValidDto();
		ClearKey(dto, keyName);

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains(keyName));
	}

	[Theory]
	[InlineData("#ZZZZZZ")]
	[InlineData("#FFFFFFFFF")]
	[InlineData("FFFFFF")]
	[InlineData("#12345")]
	public void Validate_MalformedHex_FailsNamingKey(string badHex)
	{
		var dto = CreateValidDto();
		dto.Colors!.Execution!.Depth0 = badHex;

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("depth_0") && e.Message.Contains(badHex));
	}

	[Fact]
	public void Validate_EmptyValue_FailsNamingKey()
	{
		var dto = CreateValidDto();
		dto.Colors!.Execution!.Depth0 = "";

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("depth_0"));
	}

	[Fact]
	public void Validate_WhitespaceValue_FailsNamingKey()
	{
		var dto = CreateValidDto();
		dto.Colors!.Execution!.Depth0 = "   ";

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("depth_0"));
	}

	[Fact]
	public void Validate_MultipleErrors_AllCollected()
	{
		var dto = CreateValidDto();
		dto.Colors!.Execution!.Depth0 = "not a color";
		dto.Colors.Execution.Depth1 = null;
		dto.Colors.Execution.CurrentStepMarker = "#GG";

		var result = GridStyleValidator.Validate(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
		result.Errors.Should().Contain(e => e.Message.Contains("depth_0"));
		result.Errors.Should().Contain(e => e.Message.Contains("depth_1"));
		result.Errors.Should().Contain(e => e.Message.Contains("current_step_marker"));
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
		dto.Colors!.Execution!.Depth0 = hex;

		var result = GridStyleValidator.Validate(dto);

		result.IsSuccess.Should().BeTrue();
	}

	private static GridStyleOptionsDto CreateValidDto()
	{
		return new GridStyleOptionsDto
		{
			Colors = new GridStyleColorsDto
			{
				Execution = CreateValidExecution(),
				Cells = new GridStyleCellColorsDto
				{
					Disabled = CreateValidDisabled()
				}
			}
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
			Normal = "#E0E0E0",
			Selected = "#89B4D7",
			Foreground = "#808080"
		};
	}

	private static void ClearDisabledKey(GridStyleOptionsDto dto, string keyName)
	{
		var disabled = dto.Colors!.Cells!.Disabled!;
		switch (keyName)
		{
			case "normal":
				disabled.Normal = null;
				break;
			case "selected":
				disabled.Selected = null;
				break;
			case "foreground":
				disabled.Foreground = null;
				break;
		}
	}

	private static void SetDisabledKey(GridStyleOptionsDto dto, string keyName, string value)
	{
		var disabled = dto.Colors!.Cells!.Disabled!;
		switch (keyName)
		{
			case "normal":
				disabled.Normal = value;
				break;
			case "selected":
				disabled.Selected = value;
				break;
			case "foreground":
				disabled.Foreground = value;
				break;
		}
	}

	private static void ClearKey(GridStyleOptionsDto dto, string keyName)
	{
		var exec = dto.Colors!.Execution!;
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
		}
	}
}
