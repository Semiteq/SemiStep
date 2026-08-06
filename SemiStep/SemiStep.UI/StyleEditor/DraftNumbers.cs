using System;

namespace SemiStep.UI.StyleEditor;

/// <summary>
/// Strict numeric conversions shared by the per-group drafts' <c>Build()</c> methods. A draft exposes
/// numerics as <see cref="decimal"/>? for two-way binding against a NumericUpDown, which is transiently
/// null while the operator is typing. <c>Build()</c> runs only on save — gated by the VM's
/// <c>CanSave</c>, which requires every numeric non-null and in range — so a null here means
/// <c>Build()</c> was called on a draft the guard should have rejected, and that is a bug, not a value to
/// substitute. Both methods therefore throw rather than fall back.
/// </summary>
internal static class DraftNumbers
{
	internal static int ToInt(decimal? value)
	{
		return (int)Math.Round(Require(value), MidpointRounding.AwayFromZero);
	}

	internal static double ToDouble(decimal? value)
	{
		return (double)Require(value);
	}

	private static decimal Require(decimal? value)
	{
		if (value is null)
		{
			throw new InvalidOperationException(
				"Build was called on a draft with a null numeric field; CanSave must reject a null or "
				+ "out-of-range numeric before Build runs.");
		}

		return value.Value;
	}
}
