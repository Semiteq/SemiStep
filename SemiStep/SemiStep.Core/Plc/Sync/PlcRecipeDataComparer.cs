using SemiStep.Core.Plc.State;

namespace SemiStep.Core.Plc.Sync;

internal static class PlcRecipeDataComparer
{
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
}
