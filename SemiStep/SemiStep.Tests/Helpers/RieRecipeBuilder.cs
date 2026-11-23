using SemiStep.Core.Recipes;

namespace SemiStep.Tests.Helpers;

/// <summary>Recipes valid for the shipped RIE config, with gas and gate group columns.</summary>
public static class RieRecipeBuilder
{
	private const int PumpDownActionId = 100;
	private const int GasStabilizationActionId = 200;
	private const int EtchActionId = 300;

	public static Recipe BuildThreeStepRecipe()
	{
		return Recipe.Empty
			.AppendStep(BuildPumpDownStep())
			.AppendStep(BuildGasStabilizationStep())
			.AppendStep(BuildEtchStep());
	}

	public static Step BuildPumpDownStep()
	{
		return new Step(PumpDownActionId, [])
			.WithProperty("step_duration", PropertyValue.FromFloat(10f))
			.WithProperty("comment", PropertyValue.FromString("pump"));
	}

	private static Step BuildGasStabilizationStep()
	{
		return WithGasesAndGate(new Step(GasStabilizationActionId, []))
			.WithProperty("step_duration", PropertyValue.FromFloat(15f))
			.WithProperty("comment", PropertyValue.FromString("gas"));
	}

	private static Step BuildEtchStep()
	{
		const int MatchModeManual = 2;

		return WithGasesAndGate(new Step(EtchActionId, []))
			.WithProperty("icp_power", PropertyValue.FromFloat(100f))
			.WithProperty("icp_match", PropertyValue.FromInt(MatchModeManual))
			.WithProperty("icp_load", PropertyValue.FromFloat(51f))
			.WithProperty("icp_tune", PropertyValue.FromFloat(52f))
			.WithProperty("rie_power", PropertyValue.FromFloat(200f))
			.WithProperty("rie_match", PropertyValue.FromInt(MatchModeManual))
			.WithProperty("rie_load", PropertyValue.FromFloat(61f))
			.WithProperty("rie_tune", PropertyValue.FromFloat(62f))
			.WithProperty("step_duration", PropertyValue.FromFloat(30f))
			.WithProperty("comment", PropertyValue.FromString("etch"));
	}

	private static Step WithGasesAndGate(Step step)
	{
		return step
			.WithProperty("ar", PropertyValue.FromFloat(11f))
			.WithProperty("o2", PropertyValue.FromFloat(12f))
			.WithProperty("n2", PropertyValue.FromFloat(13f))
			.WithProperty("sf6", PropertyValue.FromFloat(14f))
			.WithProperty("cf4", PropertyValue.FromFloat(15f))
			.WithProperty("cl2", PropertyValue.FromFloat(16f))
			.WithProperty("he", PropertyValue.FromFloat(17f))
			.WithProperty("gate_mode", PropertyValue.FromInt(1))
			.WithProperty("gate_setpoint", PropertyValue.FromFloat(20f));
	}
}
