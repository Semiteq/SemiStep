using System;
using System.Globalization;

namespace SemiStep.Core.Configuration;

/// <summary>
/// A grid-style color as ARGB channels, parsed from and formatted to the config's
/// <c>#RRGGBB</c> / <c>#AARRGGBB</c> hex form. <see cref="ToString"/> is manual rather than
/// deferring to a platform color type: the platform's <c>Color.ToString()</c> always emits the
/// <c>#AARRGGBB</c> form, which injects an <c>#FF</c> alpha prefix onto opaque colors and breaks
/// the config's <c>#RRGGBB</c> style on round-trip. Opaque colors (<see cref="A"/> equals
/// <c>0xFF</c>) format as <c>#RRGGBB</c>; translucent colors format as <c>#AARRGGBB</c>. Casing
/// is normalized to uppercase.
/// </summary>
public readonly record struct StyleColor(byte A, byte R, byte G, byte B)
{
	/// <summary>
	/// Parses a <c>#RRGGBB</c> or <c>#AARRGGBB</c> hex string. Throws <see cref="FormatException"/>
	/// naming the offending input when the value is not one of those two exact forms.
	/// </summary>
	public static StyleColor Parse(string value)
	{
		if (!TryParse(value, out var color))
		{
			throw new FormatException(
				$"Invalid hex color: '{value}'. Expected format: '#RRGGBB' or '#AARRGGBB'.");
		}

		return color;
	}

	/// <summary>
	/// Attempts to parse a hex color. Accepts exactly <c>#</c> followed by 6 hex digits
	/// (opaque, <see cref="A"/> set to <c>0xFF</c>) or 8 hex digits (<c>AARRGGBB</c> channel
	/// order), any letter case. Everything else — null, whitespace, missing <c>#</c>, shorthand
	/// (<c>#RGB</c> / <c>#ARGB</c>), wrong length, non-hex characters — returns <c>false</c>.
	/// </summary>
	public static bool TryParse(string? value, out StyleColor color)
	{
		color = default;

		if (string.IsNullOrWhiteSpace(value) || value[0] != '#')
		{
			return false;
		}

		if (value.Length == 7)
		{
			if (!TryParseChannel(value, 1, out var r)
				|| !TryParseChannel(value, 3, out var g)
				|| !TryParseChannel(value, 5, out var b))
			{
				return false;
			}

			color = new StyleColor(byte.MaxValue, r, g, b);
			return true;
		}

		if (value.Length == 9)
		{
			if (!TryParseChannel(value, 1, out var a)
				|| !TryParseChannel(value, 3, out var r)
				|| !TryParseChannel(value, 5, out var g)
				|| !TryParseChannel(value, 7, out var b))
			{
				return false;
			}

			color = new StyleColor(a, r, g, b);
			return true;
		}

		return false;
	}

	public override string ToString()
	{
		return A == byte.MaxValue
			? $"#{R:X2}{G:X2}{B:X2}"
			: $"#{A:X2}{R:X2}{G:X2}{B:X2}";
	}

	// AllowHexSpecifier alone is strict: it accepts only hex digits, rejecting sign and whitespace (unlike
	// NumberStyles.HexNumber, which also permits leading/trailing whitespace). The two-char span never
	// includes the leading '#', which callers strip via the fixed indices.
	private static bool TryParseChannel(string value, int index, out byte channel)
	{
		return byte.TryParse(
			value.AsSpan(index, 2),
			NumberStyles.AllowHexSpecifier,
			CultureInfo.InvariantCulture,
			out channel);
	}
}
