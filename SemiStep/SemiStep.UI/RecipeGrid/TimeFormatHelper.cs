using System.Globalization;
using System.Text.RegularExpressions;

namespace SemiStep.UI.RecipeGrid;

internal static class TimeFormatHelper
{
	internal const string TimeHmsFormat = "time_hms";
	internal const string DefaultFormatKind = "numeric";
	internal const string StepStartTimeColumnKey = "step_start_time";
	internal const string TimeUnits = "с";

	private const int SecondsPerHour = 3600;
	private const int SecondsPerMinute = 60;

	private static readonly Regex _timePattern = new(
		@"^(?<h>\d{1,2}):(?<m>\d{1,2})(:(?<s>\d{1,2})(\.(?<ms>\d+))?)?$",
		RegexOptions.Compiled);

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

		var match = _timePattern.Match(text);
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

		if (hours >= 24 || minutes >= 60 || seconds >= 60)
		{
			return text;
		}

		var totalSeconds = hours * SecondsPerHour + minutes * SecondsPerMinute + seconds + fractionalSeconds;

		return totalSeconds.ToString(CultureInfo.InvariantCulture);
	}

	internal static string FormatValue(string rawString, string? formatKind, string? units)
	{
		if (string.Equals(formatKind, TimeHmsFormat, StringComparison.OrdinalIgnoreCase)
			&& float.TryParse(rawString, NumberStyles.Float, CultureInfo.InvariantCulture, out var totalSecondsFloat))
		{
			var totalSec = (int)totalSecondsFloat;
			var hours = totalSec / SecondsPerHour;
			var minutes = (totalSec % SecondsPerHour) / SecondsPerMinute;
			var seconds = totalSec % SecondsPerMinute;
			var formatted = $"{hours:D2}:{minutes:D2}:{seconds:D2}";

			return AppendUnits(formatted, units);
		}

		return AppendUnits(rawString, units);
	}

	private static string AppendUnits(string formatted, string? units)
	{
		if (string.IsNullOrEmpty(units))
		{
			return formatted;
		}

		return $"{formatted} {units}";
	}
}
