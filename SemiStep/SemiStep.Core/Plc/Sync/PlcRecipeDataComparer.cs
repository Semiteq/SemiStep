using FluentResults;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;

namespace SemiStep.Core.Plc.Sync;

internal static class PlcRecipeDataComparer
{
	private const string LocalSide = "local";
	private const string PlcSide = "PLC";

	/// <summary>
	/// Equal when the PLC would hold the same data: a missing property encodes as its type default.
	/// </summary>
	internal static bool EncodesToSamePlcData(
		RecipeConverter recipeConverter,
		Recipe localRecipe,
		Recipe plcRecipe,
		ILogger logger)
	{
		if (localRecipe.Steps.Count != plcRecipe.Steps.Count)
		{
			return false;
		}

		for (var stepIndex = 0; stepIndex < localRecipe.Steps.Count; stepIndex++)
		{
			var localData = EncodeStep(recipeConverter, localRecipe.Steps[stepIndex], stepIndex, LocalSide, logger);
			var plcData = EncodeStep(recipeConverter, plcRecipe.Steps[stepIndex], stepIndex, PlcSide, logger);

			if (localData is null || plcData is null || !DataMatchesExpected(plcData, localData))
			{
				return false;
			}
		}

		return true;
	}

	internal static bool DataMatchesExpected(PlcRecipeData actual, PlcRecipeData expected)
	{
		if (actual.IntValues.Length != expected.IntValues.Length)
		{
			return false;
		}

		if (actual.FloatValues.Length != expected.FloatValues.Length)
		{
			return false;
		}

		if (actual.StringValues.Length != expected.StringValues.Length)
		{
			return false;
		}

		for (var i = 0; i < expected.IntValues.Length; i++)
		{
			if (actual.IntValues[i] != expected.IntValues[i])
			{
				return false;
			}
		}

		for (var i = 0; i < expected.FloatValues.Length; i++)
		{
			if (BitConverter.SingleToInt32Bits(actual.FloatValues[i]) !=
				BitConverter.SingleToInt32Bits(expected.FloatValues[i]))
			{
				return false;
			}
		}

		for (var i = 0; i < expected.StringValues.Length; i++)
		{
			if (actual.StringValues[i] != expected.StringValues[i])
			{
				return false;
			}
		}

		return true;
	}

	private static PlcRecipeData? EncodeStep(
		RecipeConverter recipeConverter,
		Step step,
		int stepIndex,
		string side,
		ILogger logger)
	{
		Result<PlcRecipeData> encoded;
		try
		{
			encoded = recipeConverter.FromRecipe(Recipe.Empty.AppendStep(step));
		}
		catch (InvalidCastException ex)
		{
			logger.LogWarning(
				ex,
				"Reconnect comparison could not encode {Side} step {StepNumber}: a value has the wrong type",
				side,
				stepIndex + 1);
			return null;
		}

		if (encoded.IsFailed)
		{
			logger.LogWarning(
				"Reconnect comparison could not encode {Side} step {StepNumber}: {Errors}",
				side,
				stepIndex + 1,
				string.Join("; ", encoded.Errors.Select(e => e.Message)));
			return null;
		}

		return encoded.Value;
	}
}
