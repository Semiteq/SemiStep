using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using FluentResults;

using NtoLib.Recipes.MbeTable.ModuleConfig.Dto.Properties;
using NtoLib.Recipes.MbeTable.ModuleCore.Reasons.Errors;

namespace NtoLib.Recipes.MbeTable.ModuleCore.Properties.Definitions;

/// <summary>
/// Time definition with hh:mm:ss.ms parsing/formatting, bounds, and seconds backing store.
/// </summary>
public sealed class DynamicTimeDefinition : ConfigurableNumericDefinition
{
	private const string TimeFormatPattern = @"^(?<h>\d{1,2}):(?<m>\d{1,2})(:(?<s>\d{1,2})(\.(?<ms>\d+))?)?$";
	private const int HoursPerDay = 24;
	private const int MinutesPerHour = 60;
	private const int SecondsPerMinute = 60;
	private const int SecondsPerHour = 3600;

	private static readonly Regex _timeRegex = new(TimeFormatPattern, RegexOptions.Compiled);

	public DynamicTimeDefinition(YamlPropertyDefinition dto) : base(dto)
	{
	}

	/// <inheritdoc/>
	public override string FormatValue(object value)
	{
		if (FormatKind != FormatKind.TimeHms)
		{
			return base.FormatValue(value);
		}

		var seconds = ToFloat(value) ?? 0f;
		var totalSeconds = (int)seconds;
		var hours = totalSeconds / SecondsPerHour;
		var minutes = (totalSeconds % SecondsPerHour) / SecondsPerMinute;
		var remainingSeconds = seconds % SecondsPerMinute;

		return $"{hours:D2}:{minutes:D2}:{remainingSeconds.ToString("00.###", CultureInfo.InvariantCulture)}";
	}

	/// <inheritdoc/>
	public override Result<object> TryParse(string input)
	{
		var sanitized = SanitizeTimeInput(input);

		if (sanitized.Contains(":"))
		{
			return ParseTimeFormat(sanitized, input);
		}

		return base.TryParse(input);
	}

	private static string SanitizeTimeInput(string input)
	{
		return new string(input
			.Where(c => char.IsDigit(c) || c == ',' || c == ':' || c == '.')
			.ToArray()).Replace(',', '.');
	}

	private static Result<object> ParseTimeFormat(string sanitized, string originalInput)
	{
		var match = _timeRegex.Match(sanitized);
		if (!match.Success)
		{
			return new CorePropertyConversionFailedError(originalInput, "time format (expected hh:mm:ss.ms)");
		}

		var hoursResult = ParseTimeComponent(match.Groups["h"].Value, "hours");
		if (hoursResult.IsFailed)
		{
			return hoursResult.ToResult();
		}

		var minutesResult = ParseTimeComponent(match.Groups["m"].Value, "minutes");
		if (minutesResult.IsFailed)
		{
			return minutesResult.ToResult();
		}

		var hours = hoursResult.Value;
		var minutes = minutesResult.Value;

		if (hours >= HoursPerDay)
		{
			return new CoreTimeComponentOutOfRangeError("hours", hours, HoursPerDay - 1);
		}

		if (minutes >= MinutesPerHour)
		{
			return new CoreTimeComponentOutOfRangeError("minutes", minutes, MinutesPerHour - 1);
		}

		var seconds = 0;
		if (match.Groups["s"].Success)
		{
			var secondsResult = ParseTimeComponent(match.Groups["s"].Value, "seconds");
			if (secondsResult.IsFailed)
			{
				return secondsResult.ToResult();
			}

			seconds = secondsResult.Value;

			if (seconds >= SecondsPerMinute)
			{
				return new CoreTimeComponentOutOfRangeError("seconds", seconds, SecondsPerMinute - 1);
			}
		}

		var milliseconds = 0f;
		if (match.Groups["ms"].Success)
		{
			if (!float.TryParse($"0.{match.Groups["ms"].Value}", NumberStyles.Float, CultureInfo.InvariantCulture,
					out milliseconds))
			{
				return new CorePropertyConversionFailedError(match.Groups["ms"].Value, "milliseconds");
			}
		}

		var totalSeconds = hours * SecondsPerHour + minutes * SecondsPerMinute + seconds + milliseconds;

		return totalSeconds;
	}

	private static Result<int> ParseTimeComponent(string value, string componentName)
	{
		if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return new CorePropertyConversionFailedError(value, componentName);
		}

		return result;
	}
}
