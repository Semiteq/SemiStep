using System.Globalization;
using System.Text.RegularExpressions;

namespace SemiStep.UI.RecipeGrid;

internal static partial class TimeFormatHelper
{
	internal const string TimeHmsFormat = "time_hms";
	internal const string DefaultFormatKind = "numeric";
	internal const string StepStartTimeColumnKey = "step_start_time";

	private const int SecondsPerHour = 3600;
	private const int SecondsPerMinute = 60;

	[GeneratedRegex(@"^(?<h>\d{1,2}):(?<m>\d{1,2})(:(?<s>\d{1,2})(\.(?<ms>\d+))?)?$")]
	private static partial Regex TimePattern();

	/// Units suffixes must be stripped by the caller before invoking this method. The editing TextBox never appends units, so no stripping is required in the editing code path.
	internal static string? ParseValue(string? text)
	{
		if (text is null)
		{
			return null;
		}

		if (!text.Contains(':'))
		{
			return text;
		}

		var match = TimePattern().Match(text);
		if (!match.Success)
		{
			return text;
		}

		var hours = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
		var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
		var seconds = match.Groups["s"].Success
			? int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture)
			: 0;
		var fractionalSeconds = match.Groups["ms"].Success
			? double.Parse($"0.{match.Groups["ms"].Value}", CultureInfo.InvariantCulture)
			: 0d;

		if (minutes >= 60 || seconds >= 60)
		{
			return text;
		}

		var totalSeconds = (hours * SecondsPerHour) + (minutes * SecondsPerMinute) + seconds + fractionalSeconds;

		return totalSeconds.ToString(CultureInfo.InvariantCulture);
	}

	// The display shows whole seconds; the editor keeps the fraction, so an unchanged edit parses back to the
	// stored value instead of rounding it down.
	internal static string FormatForEdit(string rawString, string? formatKind)
	{
		var text = FormatValue(rawString, formatKind, units: null);
		var fractionStart = rawString.IndexOf('.');
		var dropsFraction = string.Equals(formatKind, TimeHmsFormat, StringComparison.OrdinalIgnoreCase)
			&& fractionStart >= 0
			&& text != rawString;

		return dropsFraction ? text + rawString[fractionStart..] : text;
	}

	internal static string FormatValue(string rawString, string? formatKind, string? units)
	{
		if (string.Equals(formatKind, TimeHmsFormat, StringComparison.OrdinalIgnoreCase)
			&& float.TryParse(rawString, NumberStyles.Float, CultureInfo.InvariantCulture, out var totalSecondsFloat))
		{
			var totalSec = (int)totalSecondsFloat;
			var hours = totalSec / SecondsPerHour;
			var minutes = totalSec % SecondsPerHour / SecondsPerMinute;
			var seconds = totalSec % SecondsPerMinute;

			return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
		}

		return string.IsNullOrEmpty(units) ? rawString : $"{rawString} {units}";
	}
}
