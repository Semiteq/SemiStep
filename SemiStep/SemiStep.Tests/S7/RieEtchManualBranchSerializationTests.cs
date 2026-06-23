using System.Collections.Immutable;

using FluentAssertions;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

/// <summary>
/// PLC byte-layout regression for the RIE etch action 300 ("Травление") after the capacitor
/// columns (icp_load/icp_tune, rie_load/rie_tune) moved into manual-branch subactions. The
/// serializer iterates the resolved <c>Properties</c> order to fill fixed PLC slots, so the
/// slot layout must equal the layout derived from the pre-change column order. A "before"
/// baseline is impractical here (the old config is gone), so the expected slot order is
/// stated explicitly from the pre-change order and asserted against the serializer output.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "S7")]
[Trait("Area", "NestedActions")]
public sealed class RieEtchManualBranchSerializationTests
{
	private const int EtchActionId = 300;
	private const int MatchModeAuto = 1;
	private const int MatchModeManual = 2;
	private const int GateModeDefault = 1;

	[Fact]
	public async Task SerialiseEtchStep_ManualMatch_ProducesPreChangeSlotLayout()
	{
		var registry = await BuildRieRegistryAsync();
		var converter = new RecipeConverter(registry);

		var step = BuildEtchStep();
		var recipe = Recipe.Empty.AppendStep(step);

		var result = converter.FromRecipe(recipe);

		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(error => error.Message))
			: string.Empty);

		var data = result.Value;
		data.StepCount.Should().Be(1);

		// Int slots, in resolved-column order: ActionKey, then the enum columns
		// gate_mode, icp_match, rie_match.
		data.IntValues.Should().Equal(EtchActionId, 0, MatchModeManual, MatchModeManual);

		// Float slots, in resolved-column order matching the pre-change layout:
		// ar, o2, n2, sf6, cf4, cl2, he, gate_setpoint, icp_power,
		// icp_load, icp_tune, rie_power, rie_load, rie_tune, step_duration.
		data.FloatValues.Should().Equal(
			11f, 12f, 13f, 14f, 15f, 16f, 17f,
			20f,
			100f,
			51f, 52f,
			200f,
			61f, 62f,
			30f);

		// String slots: comment.
		data.StringValues.Should().Equal("etch comment");
	}

	[Fact]
	public async Task SerialiseEtchStep_AutoMatch_InactiveCapacitorColumnsWriteZeroIntoTheirSlots()
	{
		var registry = await BuildRieRegistryAsync();
		var converter = new RecipeConverter(registry);

		// Авто on both match selectors: the capacitor columns icp_load/icp_tune and
		// rie_load/rie_tune are inactive and carry no value. They must still occupy their
		// fixed float slots, written as 0, so the active columns keep their slot positions.
		var step = BuildAutoEtchStep();
		var recipe = Recipe.Empty.AppendStep(step);

		var result = converter.FromRecipe(recipe);

		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(error => error.Message))
			: string.Empty);

		var data = result.Value;

		data.IntValues.Should().Equal(EtchActionId, 0, MatchModeAuto, MatchModeAuto);

		// The four inactive capacitor slots serialise as 0; the active gas/power columns and
		// step_duration keep their exact positions (no slot shift from the dropped values).
		data.FloatValues.Should().Equal(
			11f, 12f, 13f, 14f, 15f, 16f, 17f,
			20f,
			100f,
			0f, 0f,
			200f,
			0f, 0f,
			30f);

		data.StringValues.Should().Equal("etch comment");
	}

	[Fact]
	public async Task SerialiseFreshlyCreatedEtchStep_AutoMatch_InactiveCapacitorColumnsWriteZero()
	{
		// Exercises the REAL step-creation path (StepInitializer.Create), not a hand-built step.
		// Action 300 defaults to icp_match/rie_match = Авто (the min group key), so the capacitor
		// columns icp_load/icp_tune/rie_load/rie_tune are inactive and must NOT be seeded with
		// their "50" default; their PLC slots must serialise as 0.
		var registry = await BuildRieRegistryAsync();
		var converter = new RecipeConverter(registry);

		var action = registry.GetAction(EtchActionId).Value;
		var step = StepInitializer.Create(action, registry);

		// The four capacitor columns must be absent from the freshly-created step.
		step.Properties.ContainsKey(new PropertyId("icp_load")).Should().BeFalse();
		step.Properties.ContainsKey(new PropertyId("icp_tune")).Should().BeFalse();
		step.Properties.ContainsKey(new PropertyId("rie_load")).Should().BeFalse();
		step.Properties.ContainsKey(new PropertyId("rie_tune")).Should().BeFalse();

		var recipe = Recipe.Empty.AppendStep(step);
		var result = converter.FromRecipe(recipe);

		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(error => error.Message))
			: string.Empty);

		var data = result.Value;
		// Int slots: ActionKey, then gate_mode, icp_match, rie_match — each an enum with no
		// default_value, so each seeds its group's minimum key (gate_mode min and match_mode
		// Авто both being 1 in this config).
		data.IntValues.Should().Equal(EtchActionId, GateModeDefault, MatchModeAuto, MatchModeAuto);

		// All floats default to 0 (config default_value "0" for gas/power/gate, and the inactive
		// capacitor columns left unseeded); step_duration defaults to 10.
		data.FloatValues.Should().Equal(
			0f, 0f, 0f, 0f, 0f, 0f, 0f,
			0f,
			0f,
			0f, 0f,
			0f,
			0f, 0f,
			10f);
	}

	[Fact]
	public async Task SerialiseEtchStep_RoundTrips()
	{
		var registry = await BuildRieRegistryAsync();
		var converter = new RecipeConverter(registry);

		var original = Recipe.Empty.AppendStep(BuildEtchStep());

		var serialised = converter.FromRecipe(original);
		serialised.IsSuccess.Should().BeTrue();

		var deserialised = converter.ToRecipe(serialised.Value);
		deserialised.IsSuccess.Should().BeTrue();

		deserialised.Value.Should().Be(original);
	}

	private static Step BuildEtchStep()
	{
		return new Step(EtchActionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty)
			.WithProperty("ar", PropertyValue.FromFloat(11f))
			.WithProperty("o2", PropertyValue.FromFloat(12f))
			.WithProperty("n2", PropertyValue.FromFloat(13f))
			.WithProperty("sf6", PropertyValue.FromFloat(14f))
			.WithProperty("cf4", PropertyValue.FromFloat(15f))
			.WithProperty("cl2", PropertyValue.FromFloat(16f))
			.WithProperty("he", PropertyValue.FromFloat(17f))
			.WithProperty("gate_mode", PropertyValue.FromInt(0))
			.WithProperty("gate_setpoint", PropertyValue.FromFloat(20f))
			.WithProperty("icp_power", PropertyValue.FromFloat(100f))
			.WithProperty("icp_match", PropertyValue.FromInt(MatchModeManual))
			.WithProperty("icp_load", PropertyValue.FromFloat(51f))
			.WithProperty("icp_tune", PropertyValue.FromFloat(52f))
			.WithProperty("rie_power", PropertyValue.FromFloat(200f))
			.WithProperty("rie_match", PropertyValue.FromInt(MatchModeManual))
			.WithProperty("rie_load", PropertyValue.FromFloat(61f))
			.WithProperty("rie_tune", PropertyValue.FromFloat(62f))
			.WithProperty("step_duration", PropertyValue.FromFloat(30f))
			.WithProperty("comment", PropertyValue.FromString("etch comment"));
	}

	private static Step BuildAutoEtchStep()
	{
		// Same active gas/power/duration values as BuildEtchStep, but Авто on both selectors
		// and no capacitor values set (icp_load/icp_tune, rie_load/rie_tune omitted entirely).
		return new Step(EtchActionId, ImmutableDictionary<PropertyId, PropertyValue>.Empty)
			.WithProperty("ar", PropertyValue.FromFloat(11f))
			.WithProperty("o2", PropertyValue.FromFloat(12f))
			.WithProperty("n2", PropertyValue.FromFloat(13f))
			.WithProperty("sf6", PropertyValue.FromFloat(14f))
			.WithProperty("cf4", PropertyValue.FromFloat(15f))
			.WithProperty("cl2", PropertyValue.FromFloat(16f))
			.WithProperty("he", PropertyValue.FromFloat(17f))
			.WithProperty("gate_mode", PropertyValue.FromInt(0))
			.WithProperty("gate_setpoint", PropertyValue.FromFloat(20f))
			.WithProperty("icp_power", PropertyValue.FromFloat(100f))
			.WithProperty("icp_match", PropertyValue.FromInt(MatchModeAuto))
			.WithProperty("rie_power", PropertyValue.FromFloat(200f))
			.WithProperty("rie_match", PropertyValue.FromInt(MatchModeAuto))
			.WithProperty("step_duration", PropertyValue.FromFloat(30f))
			.WithProperty("comment", PropertyValue.FromString("etch comment"));
	}

	private static async Task<RecipeMetadataRegistry> BuildRieRegistryAsync()
	{
		var path = ShippedConfigLocator.GetConfigDirectory("RIE");
		var result = await ConfigFacade.LoadAndValidateAsync(path);

		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(error => error.Message))
			: string.Empty);

		return new RecipeMetadataRegistry(result.Value);
	}
}
