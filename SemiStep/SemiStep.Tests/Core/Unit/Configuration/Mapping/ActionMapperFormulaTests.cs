using FluentAssertions;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Mapping;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Configuration.Mapping;

[Trait("Category", "Unit")]
[Trait("Component", "Config")]
[Trait("Area", "Formulas")]
public sealed class ActionMapperFormulaTests
{
	[Fact]
	public void TryMap_ValidFormula_BuildsFormulaDefinition()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "step_duration", "speed", "task", "initial_value" },
			Expressions = new Dictionary<string, string>
			{
				["step_duration"] = "(task - initial_value) / speed * 60",
				["speed"] = "(task - initial_value) / step_duration * 60",
				["task"] = "initial_value + speed * step_duration / 60",
				["initial_value"] = "task - speed * step_duration / 60"
			}
		};

		var result = ActionMapper.TryMap(dto);

		result.IsSuccess.Should().BeTrue();
		result.Value.Formula.Should().NotBeNull();
		result.Value.Formula!.RecalcOrder.Should().Equal(
			"step_duration", "speed", "task", "initial_value");
		result.Value.Formula.CompiledExpressions.Should().HaveCount(4);
		result.Value.Formula.ExpressionSources.Should().HaveCount(4);
	}

	[Fact]
	public void TryMap_NoFormula_ReturnsNullFormula()
	{
		var dto = BuildBaseActionDto();

		var result = ActionMapper.TryMap(dto);

		result.IsSuccess.Should().BeTrue();
		result.Value.Formula.Should().BeNull();
	}

	[Fact]
	public void TryMap_MissingExpressionForRecalcEntry_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed", "step_duration" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "speed * step_duration",
				["speed"] = "task / step_duration"
				// missing step_duration
			}
		};

		var result = ActionMapper.TryMap(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("missing entry for recalc_order variable 'step_duration'"));
	}

	[Fact]
	public void TryMap_ExtraExpressionKey_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "speed * 2",
				["speed"] = "task / 2",
				["mystery"] = "task + speed"
			}
		};

		var result = ActionMapper.TryMap(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'mystery'") && e.Message.Contains("not in recalc_order"));
	}

	[Fact]
	public void TryMap_UnparseableExpression_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "speed +",
				["speed"] = "task / 2"
			}
		};

		var result = ActionMapper.TryMap(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("failed to parse expression"));
	}

	[Fact]
	public void TryMap_ExpressionReferencesUnknownVariable_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "speed + zzz",
				["speed"] = "task / 2"
			}
		};

		var result = ActionMapper.TryMap(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("references variable 'zzz'")
			&& e.Message.Contains("not declared in recalc_order"));
	}

	[Fact]
	public void TryMap_RecalcOrderReferencesUnknownColumn_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "ghost" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "ghost * 2",
				["ghost"] = "task / 2"
			}
		};

		var result = ActionMapper.TryMap(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'ghost'") && e.Message.Contains("not a column"));
	}

	[Fact]
	public void TryMap_RecalcOrderHasOneEntry_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "42"
			}
		};

		var result = ActionMapper.TryMap(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("at least two entries"));
	}

	[Fact]
	public void TryMap_DuplicateRecalcOrderEntries_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed", "task" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "speed * 2",
				["speed"] = "task / 2"
			}
		};

		var result = ActionMapper.TryMap(dto);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("duplicate entries"));
	}

	private static ActionDto BuildBaseActionDto()
	{
		return new ActionDto
		{
			Id = 110,
			UiName = "t°C плавно",
			DeployDuration = "longlasting",
			Columns = new List<ActionColumnDto>
			{
				new() { Key = "task", PropertyTypeId = "temp" },
				new() { Key = "initial_value", PropertyTypeId = "temp" },
				new() { Key = "speed", PropertyTypeId = "speed" },
				new() { Key = "step_duration", PropertyTypeId = "duration" }
			}
		};
	}
}
