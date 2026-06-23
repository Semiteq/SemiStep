using FluentAssertions;

using SemiStep.Core.Recipes;

using Xunit;

namespace SemiStep.Tests.Core.Unit.Recipes;

[Trait("Category", "Unit")]
[Trait("Component", "Config")]
[Trait("Area", "NestedActions")]
public sealed class ActionTreeResolverTests
{
	[Fact]
	public void Resolve_Depth1_SplicesSubactionColumnsAfterSelector()
	{
		var icpManual = BuildSubaction(
			3002,
			"ICP manual",
			Column("icp_load", "percent"),
			Column("icp_tune", "percent"));

		var rieManual = BuildSubaction(
			3003,
			"RIE manual",
			Column("rie_load", "percent"),
			Column("rie_tune", "percent"));

		var root = BuildAction(
			300,
			"Etch",
			Column("icp_power", "power"),
			Selector("icp_match", "enum", (2, 3002)),
			Selector("rie_match", "enum", (2, 3003)),
			Column("step_duration", "time"),
			Column("comment", "string"));

		var result = ActionTreeResolver.Resolve(new[] { root, icpManual, rieManual });

		result.IsSuccess.Should().BeTrue();
		var resolved = result.Value.Single();
		Keys(resolved).Should().Equal(
			"icp_power", "icp_match", "icp_load", "icp_tune",
			"rie_match", "rie_load", "rie_tune", "step_duration", "comment");
	}

	[Fact]
	public void Resolve_Depth1_AssignsActivationConditionsToSubactionColumns()
	{
		var icpManual = BuildSubaction(3002, "ICP manual", Column("icp_load", "percent"));
		var root = BuildAction(
			300,
			"Etch",
			Column("icp_power", "power"),
			Selector("icp_match", "enum", (2, 3002)));

		var result = ActionTreeResolver.Resolve(new[] { root, icpManual });

		result.IsSuccess.Should().BeTrue();
		var resolved = result.Value.Single();

		Property(resolved, "icp_power").Activation.Should().BeNull();
		Property(resolved, "icp_match").Activation.Should().BeNull();

		var icpLoad = Property(resolved, "icp_load");
		icpLoad.Activation.Should().ContainSingle()
			.Which.Should().Be(new ActivationCondition("icp_match", 2));
	}

	[Fact]
	public void Resolve_ResolvedColumns_DropTargets()
	{
		var icpManual = BuildSubaction(3002, "ICP manual", Column("icp_load", "percent"));
		var root = BuildAction(300, "Etch", Selector("icp_match", "enum", (2, 3002)));

		var result = ActionTreeResolver.Resolve(new[] { root, icpManual });

		result.IsSuccess.Should().BeTrue();
		result.Value.Single().Properties.Should().OnlyContain(p => p.Targets == null);
	}

	[Fact]
	public void Resolve_Depth2_ChamberToCriterion_BuildsChainedActivation()
	{
		var byPressure = BuildSubaction(
			4002,
			"By pressure",
			Column("target_pressure", "pressure"));

		var byTime = BuildSubaction(
			4003,
			"By time",
			Column("target_time", "time"));

		var chamber = BuildSubaction(
			4001,
			"Chamber",
			Column("chamber_id", "enum"),
			Selector("criterion", "enum", (1, 4002), (2, 4003)));

		var root = BuildAction(
			400,
			"Pump",
			Selector("destination", "enum", (1, 4001)),
			Column("comment", "string"));

		var result = ActionTreeResolver.Resolve(new[] { root, chamber, byPressure, byTime });

		result.IsSuccess.Should().BeTrue();
		var resolved = result.Value.Single();

		Keys(resolved).Should().Equal(
			"destination", "chamber_id", "criterion",
			"target_pressure", "target_time", "comment");

		Property(resolved, "chamber_id").Activation.Should().ContainSingle()
			.Which.Should().Be(new ActivationCondition("destination", 1));

		Property(resolved, "target_pressure").Activation.Should().Equal(
			new ActivationCondition("destination", 1),
			new ActivationCondition("criterion", 1));

		Property(resolved, "target_time").Activation.Should().Equal(
			new ActivationCondition("destination", 1),
			new ActivationCondition("criterion", 2));

		Property(resolved, "comment").Activation.Should().BeNull();
	}

	[Fact]
	public void Resolve_SharedSubactionAcrossBranchesOfSameRoot_Fails()
	{
		// 5002 is reachable from two selectors of the SAME root with different activation
		// conditions. Only the first path's conditions would survive on the resolved column,
		// silently greying it wrongly under the second branch. The resolver rejects this.
		var shared = BuildSubaction(
			5002,
			"Shared",
			Column("shared_a", "percent"),
			Column("shared_b", "percent"));

		var root = BuildAction(
			500,
			"Process",
			Selector("mode_one", "enum", (2, 5002)),
			Selector("mode_two", "enum", (2, 5002)),
			Column("tail", "string"));

		var result = ActionTreeResolver.Resolve(new[] { root, shared });

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("more than one selector condition", StringComparison.OrdinalIgnoreCase)
			&& e.Message.Contains("shared_a", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Resolve_SameSelectorTwoValuesToSameSubaction_Fails()
	{
		// One selector maps two distinct values to the SAME subaction (targets: {2: X, 3: X}).
		// The two values yield different activation paths reaching the same columns; representing
		// OR-of-paths is unsupported, so the resolver rejects the meaningless authoring.
		var shared = BuildSubaction(
			5002,
			"Shared",
			Column("shared_a", "percent"),
			Column("shared_b", "percent"));

		var root = BuildAction(
			500,
			"Process",
			Selector("mode", "enum", (2, 5002), (3, 5002)),
			Column("tail", "string"));

		var result = ActionTreeResolver.Resolve(new[] { root, shared });

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("more than one selector condition", StringComparison.OrdinalIgnoreCase)
			&& e.Message.Contains("shared_a", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Resolve_SharedSubactionAcrossDistinctRoots_ResolvesOncePerRoot()
	{
		// The same subaction shared by two DIFFERENT roots is allowed: each root is resolved
		// independently with its own activation path.
		var shared = BuildSubaction(
			5002,
			"Shared",
			Column("shared_a", "percent"));

		var rootOne = BuildAction(
			500,
			"ProcessOne",
			Selector("mode_one", "enum", (2, 5002)));

		var rootTwo = BuildAction(
			501,
			"ProcessTwo",
			Selector("mode_two", "enum", (3, 5002)));

		var result = ActionTreeResolver.Resolve(new[] { rootOne, rootTwo, shared });

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().HaveCount(2);

		var resolvedOne = result.Value.Single(a => a.Id == 500);
		var resolvedTwo = result.Value.Single(a => a.Id == 501);

		Property(resolvedOne, "shared_a").Activation.Should().ContainSingle()
			.Which.Should().Be(new ActivationCondition("mode_one", 2));
		Property(resolvedTwo, "shared_a").Activation.Should().ContainSingle()
			.Which.Should().Be(new ActivationCondition("mode_two", 3));
	}

	[Fact]
	public void Resolve_NoRoots_ReturnsEmpty()
	{
		var orphanSubaction = BuildSubaction(9001, "Lonely", Column("x", "percent"));

		var result = ActionTreeResolver.Resolve(new[] { orphanSubaction });

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().BeEmpty();
	}

	[Fact]
	public void Resolve_DirectCycle_Fails()
	{
		var subA = BuildSubaction(6002, "A", Selector("link", "enum", (1, 6003)));
		var subB = BuildSubaction(6003, "B", Selector("back", "enum", (1, 6002)));
		var root = BuildAction(600, "Root", Selector("enter", "enum", (1, 6002)));

		var result = ActionTreeResolver.Resolve(new[] { root, subA, subB });

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("Cycle detected"));
	}

	[Fact]
	public void Resolve_ConflictingPropertyTypes_Fails()
	{
		var subOne = BuildSubaction(7002, "One", Column("shared", "percent"));
		var subTwo = BuildSubaction(7003, "Two", Column("shared", "time"));
		var root = BuildAction(
			700,
			"Root",
			Selector("a", "enum", (1, 7002)),
			Selector("b", "enum", (1, 7003)));

		var result = ActionTreeResolver.Resolve(new[] { root, subOne, subTwo });

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("conflicting property types"));
	}

	[Fact]
	public void Resolve_DanglingTarget_Fails()
	{
		var root = BuildAction(800, "Root", Selector("sel", "enum", (1, 9999)));

		var result = ActionTreeResolver.Resolve(new[] { root });

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e => e.Message.Contains("undefined action id 9999"));
	}

	[Fact]
	public void Resolve_DeterministicOrder_StableAcrossLoads()
	{
		var icpManual = BuildSubaction(3002, "ICP manual", Column("icp_load", "percent"), Column("icp_tune", "percent"));
		var rieManual = BuildSubaction(3003, "RIE manual", Column("rie_load", "percent"), Column("rie_tune", "percent"));

		var first = ActionTreeResolver.Resolve(new[]
		{
			BuildAction(300, "Etch", Column("icp_power", "power"), Selector("icp_match", "enum", (2, 3002)), Selector("rie_match", "enum", (2, 3003))),
			icpManual,
			rieManual
		});

		var second = ActionTreeResolver.Resolve(new[]
		{
			rieManual,
			icpManual,
			BuildAction(300, "Etch", Column("icp_power", "power"), Selector("icp_match", "enum", (2, 3002)), Selector("rie_match", "enum", (2, 3003)))
		});

		first.IsSuccess.Should().BeTrue();
		second.IsSuccess.Should().BeTrue();
		Keys(first.Value.Single()).Should().Equal(Keys(second.Value.Single()));
	}

	[Fact]
	public void Resolve_OnlyRootsAreReturned_SubactionsExcluded()
	{
		var sub = BuildSubaction(3002, "ICP manual", Column("icp_load", "percent"));
		var root = BuildAction(300, "Etch", Selector("icp_match", "enum", (2, 3002)));

		var result = ActionTreeResolver.Resolve(new[] { root, sub });

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().ContainSingle().Which.Id.Should().Be(300);
	}

	private static IReadOnlyList<string> Keys(ActionDefinition action)
	{
		return action.Properties.Select(p => p.Key).ToList();
	}

	private static ActionPropertyDefinition Property(ActionDefinition action, string key)
	{
		return action.Properties.Single(p => p.Key == key);
	}

	private static ActionPropertyDefinition Column(string key, string propertyTypeId)
	{
		return new ActionPropertyDefinition(key, null, propertyTypeId, null);
	}

	private static ActionPropertyDefinition Selector(
		string key,
		string propertyTypeId,
		params (int Value, int TargetId)[] targets)
	{
		var map = targets.ToDictionary(t => t.Value, t => t.TargetId);
		return new ActionPropertyDefinition(key, "match_mode", propertyTypeId, null, map);
	}

	private static ActionDefinition BuildAction(int id, string uiName, params ActionPropertyDefinition[] columns)
	{
		return new ActionDefinition(id, uiName, DeployDuration.LongLasting, columns, null, ActionRole.Action);
	}

	private static ActionDefinition BuildSubaction(int id, string uiName, params ActionPropertyDefinition[] columns)
	{
		return new ActionDefinition(id, uiName, DeployDuration.LongLasting, columns, null, ActionRole.Subaction);
	}
}
