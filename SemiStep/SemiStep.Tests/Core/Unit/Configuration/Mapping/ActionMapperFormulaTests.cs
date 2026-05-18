using FluentAssertions;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Core.Recipes;

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

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsSuccess.Should().BeTrue();
		result.Value.Formula.Should().NotBeNull();
		result.Value.Formula!.RecalcOrder.Should().Equal(
			"step_duration", "speed", "task", "initial_value");
		result.Value.Formula.CompiledExpressions.Should().HaveCount(4);
	}

	[Fact]
	public void TryMap_NoFormula_ReturnsNullFormula()
	{
		var dto = BuildBaseActionDto();

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

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

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

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

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

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

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

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

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

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

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

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

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

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

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("duplicate entries"));
	}

	[Fact]
	public void TryMap_RecalcOrderIncludesNonNumericColumn_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Columns!.Add(new ActionColumnDto { Key = "comment", PropertyTypeId = "string" });
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "comment" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "comment + 1",
				["comment"] = "task - 1"
			}
		};

		var properties = new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["temp"] = new PropertyTypeDefinition("temp", "float", "decimal", null, null, null, null),
			["speed"] = new PropertyTypeDefinition("speed", "float", "decimal", null, null, null, null),
			["duration"] = new PropertyTypeDefinition("duration", "float", "decimal", null, null, null, null),
			["string"] = new PropertyTypeDefinition("string", "string", "decimal", null, null, null, 255)
		};

		var result = ActionMapper.TryMap(dto, properties);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("non-numeric system_type"));
	}

	[Fact]
	public void TryMap_NormalizesRecalcOrderCaseToColumnKeyCasing()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			// recalc_order uses different casing than column keys
			RecalcOrder = new List<string> { "STEP_DURATION", "SPEED", "TASK", "INITIAL_VALUE" },
			Expressions = new Dictionary<string, string>
			{
				["STEP_DURATION"] = "(task - initial_value) / speed * 60",
				["SPEED"] = "(task - initial_value) / step_duration * 60",
				["TASK"] = "initial_value + speed * step_duration / 60",
				["INITIAL_VALUE"] = "task - speed * step_duration / 60"
			}
		};

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsSuccess.Should().BeTrue();
		// Canonical casing matches column keys ("task", "speed", "step_duration", "initial_value"), not the YAML input.
		result.Value.Formula!.RecalcOrder.Should().Equal(
			"step_duration", "speed", "task", "initial_value");
		result.Value.Formula.CompiledExpressions.Should().ContainKey("step_duration");
		result.Value.Formula.CompiledExpressions.Should().NotContainKey("STEP_DURATION");
	}

	[Fact]
	public void TryMap_MultipleFormulaDefects_AggregatesAllErrors()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed", "ghost" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "speed +",
				["speed"] = "task / 2",
				["ghost"] = "task"
			}
		};

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsFailed.Should().BeTrue();
		// Both the unknown-column error and the parse error should appear.
		result.Errors.Should().Contain(e => e.Message.Contains("'ghost'") && e.Message.Contains("not a column"));
		result.Errors.Should().Contain(e => e.Message.Contains("failed to parse expression"));
	}

	[Fact]
	public void TryMap_EmptyFormulaBlock_FailsOnRecalcOrder()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = null,
			Expressions = null
		};

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("at least two entries"));
	}

	[Fact]
	public void TryMap_RecalcOrderWithoutExpressions_FailsForEachMissing()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed" },
			Expressions = null
		};

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("missing entry for recalc_order variable 'task'"));
		result.Errors.Should().Contain(e => e.Message.Contains("missing entry for recalc_order variable 'speed'"));
	}

	[Fact]
	public void TryMap_ExpressionIdentifierCaseDiffersFromRecalcOrder_Fails()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed", "step_duration", "initial_value" },
			Expressions = new Dictionary<string, string>
			{
				// 'TASK' inside the expression body has different casing than recalc_order entry 'task'.
				["step_duration"] = "(TASK - initial_value) / speed * 60",
				["speed"] = "(task - initial_value) / step_duration * 60",
				["task"] = "initial_value + speed * step_duration / 60",
				["initial_value"] = "task - speed * step_duration / 60"
			}
		};

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("'TASK'") && e.Message.Contains("casing"));
	}

	[Fact]
	public void TryMap_ExpressionKeyDifferingInCase_FailsAsDuplicate()
	{
		var dto = BuildBaseActionDto();
		dto.Formula = new FormulaDto
		{
			RecalcOrder = new List<string> { "task", "speed" },
			Expressions = new Dictionary<string, string>
			{
				["task"] = "speed * 2",
				["TASK"] = "speed * 3",
				["speed"] = "task / 2"
			}
		};

		var result = ActionMapper.TryMap(dto, BuildDefaultProperties());

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("duplicate entry") && e.Message.Contains("TASK"));
	}

	private static IReadOnlyDictionary<string, PropertyTypeDefinition> BuildDefaultProperties()
	{
		return new Dictionary<string, PropertyTypeDefinition>(StringComparer.OrdinalIgnoreCase)
		{
			["temp"] = new PropertyTypeDefinition("temp", "float", "decimal", null, null, null, null),
			["speed"] = new PropertyTypeDefinition("speed", "float", "decimal", null, null, null, null),
			["duration"] = new PropertyTypeDefinition("duration", "float", "decimal", null, null, null, null),
			["string"] = new PropertyTypeDefinition("string", "string", "decimal", null, null, null, 255)
		};
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
