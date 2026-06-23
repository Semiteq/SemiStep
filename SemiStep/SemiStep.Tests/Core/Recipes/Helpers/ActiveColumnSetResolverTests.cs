using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Recipes.Helpers;

[Trait("Component", "Core")]
[Trait("Category", "Unit")]
[Trait("Area", "NestedActions")]
public sealed class ActiveColumnSetResolverTests
{
	private const int Auto = 1;
	private const int Manual = 2;

	[Fact]
	public void Resolve_IcpMatchAuto_IcpManualColumnsInactive()
	{
		var action = BuildRieAction();
		var step = BuildStep(icpMatch: Auto, rieMatch: Auto);

		var active = ActiveColumnSetResolver.Resolve(action, step);

		active.Should().NotContain("icp_load");
		active.Should().NotContain("icp_tune");
	}

	[Fact]
	public void Resolve_IcpMatchManual_IcpManualColumnsActive()
	{
		var action = BuildRieAction();
		var step = BuildStep(icpMatch: Manual, rieMatch: Auto);

		var active = ActiveColumnSetResolver.Resolve(action, step);

		active.Should().Contain("icp_load");
		active.Should().Contain("icp_tune");
	}

	[Fact]
	public void Resolve_RieBranchIndependentOfIcpBranch()
	{
		var action = BuildRieAction();
		var step = BuildStep(icpMatch: Auto, rieMatch: Manual);

		var active = ActiveColumnSetResolver.Resolve(action, step);

		active.Should().NotContain("icp_load");
		active.Should().NotContain("icp_tune");
		active.Should().Contain("rie_load");
		active.Should().Contain("rie_tune");
	}

	[Fact]
	public void Resolve_AlwaysActiveColumns_StayActiveRegardlessOfSelectors()
	{
		var action = BuildRieAction();
		var step = BuildStep(icpMatch: Auto, rieMatch: Auto);

		var active = ActiveColumnSetResolver.Resolve(action, step);

		active.Should().Contain("icp_match");
		active.Should().Contain("rie_match");
		active.Should().Contain("step_duration");
	}

	[Fact]
	public void Resolve_SelectorValueMissing_DependentColumnsInactive()
	{
		var action = BuildRieAction();
		var step = new Step(300, ImmutableDictionary<PropertyId, PropertyValue>.Empty);

		var active = ActiveColumnSetResolver.Resolve(action, step);

		active.Should().NotContain("icp_load");
		active.Should().NotContain("rie_load");
		active.Should().Contain("step_duration");
	}

	[Fact]
	public void Resolve_Depth2Chain_RequiresEveryConditionOnThePath()
	{
		// chamber selector value 5 -> branch, then criterion selector value 7 -> leaf column.
		var action = BuildDepth2Action();

		var both = ActiveColumnSetResolver.Resolve(
			action,
			BuildDepth2Step(chamber: 5, criterion: 7));
		both.Should().Contain("leaf_value");

		var chamberOnly = ActiveColumnSetResolver.Resolve(
			action,
			BuildDepth2Step(chamber: 5, criterion: 0));
		chamberOnly.Should().NotContain("leaf_value");

		var none = ActiveColumnSetResolver.Resolve(
			action,
			BuildDepth2Step(chamber: 0, criterion: 7));
		none.Should().NotContain("leaf_value");
	}

	private static ActionDefinition BuildRieAction()
	{
		var properties = new List<ActionPropertyDefinition>
		{
			Enum("icp_match"),
			Percent("icp_load", new ActivationCondition("icp_match", Manual)),
			Percent("icp_tune", new ActivationCondition("icp_match", Manual)),
			Enum("rie_match"),
			Percent("rie_load", new ActivationCondition("rie_match", Manual)),
			Percent("rie_tune", new ActivationCondition("rie_match", Manual)),
			Time("step_duration")
		};

		return new ActionDefinition(
			id: 300,
			uiName: "Travlenie",
			deployDuration: DeployDuration.LongLasting,
			properties: properties);
	}

	private static ActionDefinition BuildDepth2Action()
	{
		var properties = new List<ActionPropertyDefinition>
		{
			Enum("chamber"),
			Enum("criterion", new ActivationCondition("chamber", 5)),
			Percent(
				"leaf_value",
				new ActivationCondition("chamber", 5),
				new ActivationCondition("criterion", 7))
		};

		return new ActionDefinition(
			id: 400,
			uiName: "Pump",
			deployDuration: DeployDuration.LongLasting,
			properties: properties);
	}

	private static Step BuildStep(int icpMatch, int rieMatch)
	{
		var properties = ImmutableDictionary<PropertyId, PropertyValue>.Empty
			.SetItem(new PropertyId("icp_match"), PropertyValue.FromInt(icpMatch))
			.SetItem(new PropertyId("rie_match"), PropertyValue.FromInt(rieMatch));

		return new Step(300, properties);
	}

	private static Step BuildDepth2Step(int chamber, int criterion)
	{
		var properties = ImmutableDictionary<PropertyId, PropertyValue>.Empty
			.SetItem(new PropertyId("chamber"), PropertyValue.FromInt(chamber))
			.SetItem(new PropertyId("criterion"), PropertyValue.FromInt(criterion));

		return new Step(400, properties);
	}

	private static ActionPropertyDefinition Enum(string key, params ActivationCondition[] activation)
	{
		return new ActionPropertyDefinition(
			Key: key,
			GroupName: null,
			PropertyTypeId: "enum",
			DefaultValue: null,
			Targets: null,
			Activation: activation.Length == 0 ? null : activation);
	}

	private static ActionPropertyDefinition Percent(string key, params ActivationCondition[] activation)
	{
		return new ActionPropertyDefinition(
			Key: key,
			GroupName: null,
			PropertyTypeId: "percent",
			DefaultValue: "50",
			Targets: null,
			Activation: activation.Length == 0 ? null : activation);
	}

	private static ActionPropertyDefinition Time(string key)
	{
		return new ActionPropertyDefinition(
			Key: key,
			GroupName: null,
			PropertyTypeId: "time",
			DefaultValue: "10");
	}
}
