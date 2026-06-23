using FluentAssertions;

using FluentResults;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Configuration.Validation;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Configuration.Validation;

/// <summary>
/// Direct unit coverage of the reference-graph rules in <see cref="CrossReferenceValidator"/>,
/// driven by a hand-built <see cref="ActionDto"/> list. The integration tests exercise the same
/// rules through the full config load, but that path also runs the resolver, which independently
/// rejects dangling targets and cycles; these tests pin each validator rule and its specific
/// message in isolation so a regression that removes a rule cannot hide behind the resolver.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Config")]
[Trait("Area", "NestedActions")]
public sealed class CrossReferenceValidatorGraphTests
{
	[Fact]
	public void DanglingTarget_Fails_WithSpecificMessage()
	{
		var actions = new List<ActionDto>
		{
			Action(300, "Root", Selector("sel", 1, 9999))
		};

		var result = Validate(actions);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("targets undefined action id 9999", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void TargetIsAction_Fails_WithSpecificMessage()
	{
		var actions = new List<ActionDto>
		{
			Action(300, "Root", Selector("sel", 1, 400)),
			Action(400, "OtherRoot")
		};

		var result = Validate(actions);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("which is role 'action'", StringComparison.OrdinalIgnoreCase)
			&& e.Message.Contains("must point at a 'subaction'", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void TargetIsAction_DeeperThanDepth1_StillFails()
	{
		// A subaction's OWN selector targets a role:action (a mis-tagged fragment deeper than
		// depth-1). The rule iterates every action's columns, so it must fire at any depth.
		var actions = new List<ActionDto>
		{
			Action(300, "Root", Selector("sel", 1, 3002)),
			Subaction(3002, "Branch", Selector("deeper", 1, 400)),
			Action(400, "AnotherRoot")
		};

		var result = Validate(actions);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("targets action id 400", StringComparison.OrdinalIgnoreCase)
			&& e.Message.Contains("must point at a 'subaction'", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void OrphanSubaction_Fails_WithSpecificMessage()
	{
		var actions = new List<ActionDto>
		{
			Action(300, "Root"),
			Subaction(3001, "Unreferenced", new ActionColumnDto { Key = "x", PropertyTypeId = "percent" })
		};

		var result = Validate(actions);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("orphan subaction", StringComparison.OrdinalIgnoreCase)
			&& e.Message.Contains("3001", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Cycle_Fails_WithSpecificMessage()
	{
		var actions = new List<ActionDto>
		{
			Action(300, "Root", Selector("enter", 1, 3002)),
			Subaction(3002, "A", Selector("link", 1, 3003)),
			Subaction(3003, "B", Selector("back", 1, 3002))
		};

		var result = Validate(actions);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Cycle detected", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void ValidNestedGraph_Passes()
	{
		var actions = new List<ActionDto>
		{
			Action(300, "Root", Selector("sel", 1, 3002)),
			Subaction(3002, "Manual", new ActionColumnDto { Key = "x", PropertyTypeId = "percent" })
		};

		var result = Validate(actions);

		result.IsSuccess.Should().BeTrue();
	}

	private static Result Validate(List<ActionDto> actions)
	{
		// Supply the supporting properties/columns/groups every referenced key needs so that only
		// the reference-graph rules under test can fail; the column/property/group existence rules
		// (validated by sibling methods) stay green.
		var properties = new List<PropertyDto>
		{
			new() { PropertyTypeId = "enum" },
			new() { PropertyTypeId = "percent" },
			new() { PropertyTypeId = "time" },
			new() { PropertyTypeId = "string" }
		};

		var columnKeys = actions
			.Where(action => action.Columns != null)
			.SelectMany(action => action.Columns!)
			.Select(column => column.Key!)
			.Distinct()
			.ToList();

		var columns = columnKeys
			.Select(key => new ColumnDto { Key = key })
			.ToList();

		var groups = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase)
		{
			["match_mode"] = new() { [0] = "Auto", [1] = "Manual" }
		};

		return CrossReferenceValidator.Validate(properties, columns, groups, actions);
	}

	private static ActionDto Action(int id, string uiName, params ActionColumnDto[] columns)
	{
		return new ActionDto
		{
			Id = id,
			UiName = uiName,
			Role = "action",
			Columns = columns.ToList()
		};
	}

	private static ActionDto Subaction(int id, string uiName, params ActionColumnDto[] columns)
	{
		return new ActionDto
		{
			Id = id,
			UiName = uiName,
			Role = "subaction",
			Columns = columns.ToList()
		};
	}

	private static ActionColumnDto Selector(string key, int selectorValue, int targetId)
	{
		return new ActionColumnDto
		{
			Key = key,
			PropertyTypeId = "enum",
			GroupName = "match_mode",
			Targets = new Dictionary<int, int> { [selectorValue] = targetId }
		};
	}
}
