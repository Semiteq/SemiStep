using TypesShared.Plc;

namespace S7.Sync;

internal static class PlcRecipeDataComparer
{
	// Floats are serialised to raw IEEE 754 bytes, written to the PLC, and read back without
	// any arithmetic transformation. The round-trip is byte-exact, so bit-exact equality is
	// the correct comparison here — not an epsilon-based approximation.
	// BitConverter.SingleToInt32Bits is used rather than == to correctly handle NaN payloads
	// and distinguish +0 from -0 (different IEEE-754 bit patterns).
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
