using FluentAssertions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.NestedActions;

/// <summary>
/// Verifies the real shipped RIE config (<c>ConfigFiles/RIE</c>): the etch action 300
/// ("Травление") resolves its capacitor columns through the icp_match / rie_match manual
/// branches, the resolved column union reproduces the pre-change order byte-for-byte, and
/// the capacitor columns are active only when their match selector is Ручной (value 2).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "NestedActions")]
public sealed class RieEtchManualBranchConfigTests
{
	private const int EtchActionId = 300;
	private const int MatchModeAuto = 1;
	private const int MatchModeManual = 2;

	// The exact column order action 300 produced before capacitor columns became subactions.
	// The PLC writes values into fixed slots by this order, so the resolved union must match
	// it byte-for-byte (rie_power stays between the icp splice and rie_match).
	private static string[] ExpectedEtchColumnOrder()
	{
		return
		[
			"ar", "o2", "n2", "sf6", "cf4", "cl2", "he",
			"gate_mode", "gate_setpoint",
			"icp_power", "icp_match", "icp_load", "icp_tune",
			"rie_power", "rie_match", "rie_load", "rie_tune",
			"step_duration", "comment"
		];
	}

	[Fact]
	public async Task RieConfig_LoadsAndValidatesSuccessfully()
	{
		var result = await ConfigFacade.LoadAndValidateAsync(RieConfigPath());

		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(error => error.Message))
			: string.Empty);
	}

	[Fact]
	public async Task EtchAction_ResolvedUnion_MatchesPriorColumnOrderExactly()
	{
		var registry = await BuildRieRegistryAsync();

		var etch = registry.GetAction(EtchActionId);
		etch.IsSuccess.Should().BeTrue();

		etch.Value.Properties.Select(property => property.Key)
			.Should().Equal(ExpectedEtchColumnOrder());
	}

	[Fact]
	public async Task EtchAction_DropsTargetsFromResolvedColumns()
	{
		var registry = await BuildRieRegistryAsync();

		var etch = registry.GetAction(EtchActionId).Value;

		etch.Properties.Should().OnlyContain(property => property.Targets == null);
	}

	[Fact]
	public async Task EtchAction_CapacitorColumns_CarryManualBranchActivation()
	{
		var registry = await BuildRieRegistryAsync();
		var etch = registry.GetAction(EtchActionId).Value;

		Activation(etch, "icp_load").Should().ContainSingle()
			.Which.Should().Be(new ActivationCondition("icp_match", MatchModeManual));
		Activation(etch, "icp_tune").Should().ContainSingle()
			.Which.Should().Be(new ActivationCondition("icp_match", MatchModeManual));
		Activation(etch, "rie_load").Should().ContainSingle()
			.Which.Should().Be(new ActivationCondition("rie_match", MatchModeManual));
		Activation(etch, "rie_tune").Should().ContainSingle()
			.Which.Should().Be(new ActivationCondition("rie_match", MatchModeManual));

		Property(etch, "icp_power").Activation.Should().BeNull();
		Property(etch, "rie_power").Activation.Should().BeNull();
		Property(etch, "icp_match").Activation.Should().BeNull();
		Property(etch, "rie_match").Activation.Should().BeNull();
	}

	[Fact]
	public async Task EtchAction_AutoMatch_DeactivatesCapacitorColumns()
	{
		var registry = await BuildRieRegistryAsync();
		var etch = registry.GetAction(EtchActionId).Value;

		var step = StepWithMatchModes(icpMatch: MatchModeAuto, rieMatch: MatchModeAuto);
		var active = ActiveColumnSetResolver.Resolve(etch, step);

		active.Should().NotContain("icp_load");
		active.Should().NotContain("icp_tune");
		active.Should().NotContain("rie_load");
		active.Should().NotContain("rie_tune");
		active.Should().Contain("icp_power");
		active.Should().Contain("rie_power");
	}

	[Fact]
	public async Task EtchAction_ManualMatch_ActivatesCapacitorColumns()
	{
		var registry = await BuildRieRegistryAsync();
		var etch = registry.GetAction(EtchActionId).Value;

		var step = StepWithMatchModes(icpMatch: MatchModeManual, rieMatch: MatchModeManual);
		var active = ActiveColumnSetResolver.Resolve(etch, step);

		active.Should().Contain("icp_load");
		active.Should().Contain("icp_tune");
		active.Should().Contain("rie_load");
		active.Should().Contain("rie_tune");
	}

	[Fact]
	public async Task EtchAction_MatchSelectorsAreIndependent()
	{
		var registry = await BuildRieRegistryAsync();
		var etch = registry.GetAction(EtchActionId).Value;

		var step = StepWithMatchModes(icpMatch: MatchModeManual, rieMatch: MatchModeAuto);
		var active = ActiveColumnSetResolver.Resolve(etch, step);

		active.Should().Contain("icp_load");
		active.Should().Contain("icp_tune");
		active.Should().NotContain("rie_load");
		active.Should().NotContain("rie_tune");
	}

	[Fact]
	public async Task RieConfig_DropdownExcludesSubactions()
	{
		var registry = await BuildRieRegistryAsync();

		var dropdownIds = registry.GetActionComboBoxItems().Select(item => item.Id).ToList();

		dropdownIds.Should().NotContain(3002);
		dropdownIds.Should().NotContain(3003);
		dropdownIds.Should().Contain(EtchActionId);
	}

	private static Step StepWithMatchModes(int icpMatch, int rieMatch)
	{
		return new Step(EtchActionId, System.Collections.Immutable.ImmutableDictionary<PropertyId, PropertyValue>.Empty)
			.WithProperty("icp_match", PropertyValue.FromInt(icpMatch))
			.WithProperty("rie_match", PropertyValue.FromInt(rieMatch));
	}

	private static IReadOnlyList<ActivationCondition>? Activation(ActionDefinition action, string key)
	{
		return Property(action, key).Activation;
	}

	private static ActionPropertyDefinition Property(ActionDefinition action, string key)
	{
		return action.Properties.Single(property => property.Key == key);
	}

	private static async Task<RecipeMetadataRegistry> BuildRieRegistryAsync()
	{
		var result = await ConfigFacade.LoadAndValidateAsync(RieConfigPath());
		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(error => error.Message))
			: string.Empty);

		return new RecipeMetadataRegistry(result.Value);
	}

	private static string RieConfigPath()
	{
		return ShippedConfigLocator.GetConfigDirectory("RIE");
	}
}
