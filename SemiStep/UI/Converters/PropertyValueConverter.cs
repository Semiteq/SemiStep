using System.Globalization;
using System.Text.RegularExpressions;

using Avalonia.Data.Converters;

namespace UI.Converters;

public sealed partial class PropertyValueConverter(string formatKind, string? units, bool appendUnits = true) : IValueConverter
{
	private const string TimeHmsFormat = "time_hms";
	private const int SecondsPerHour = 3600;
	private const int SecondsPerMinute = 60;

	private readonly string _formatKind = formatKind;
	private readonly string? _units = units;
	private readonly bool _appendUnits = appendUnits;

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is null)
		{
			return string.Empty;
		}

		var rawString = value.ToString();
		if (string.IsNullOrEmpty(rawString))
		{
			return string.Empty;
		}

		if (string.Equals(_formatKind, TimeHmsFormat, StringComparison.OrdinalIgnoreCase))
		{
			if (float.TryParse(rawString, CultureInfo.InvariantCulture, out var totalSecondsFloat))
			{
				var totalSec = (int)totalSecondsFloat;
				var hours = totalSec / SecondsPerHour;
				var minutes = (totalSec % SecondsPerHour) / SecondsPerMinute;
				var seconds = totalSec % SecondsPerMinute;
				var formatted = $"{hours:D2}:{minutes:D2}:{seconds:D2}";

				return _appendUnits ? AppendUnits(formatted) : formatted;
			}

			return _appendUnits ? AppendUnits(rawString) : rawString;
		}

		return _appendUnits ? AppendUnits(rawString) : rawString;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is null)
		{
			return null;
		}

		var text = value.ToString()?.Trim();
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}

		text = StripUnitsSuffix(text);

		if (string.Equals(_formatKind, TimeHmsFormat, StringComparison.OrdinalIgnoreCase)
			&& text.Contains(':'))
		{
			var match = TimePattern().Match(text);
			if (match.Success)
			{
				var h = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
				var m = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
				var s = match.Groups["s"].Success
					? int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture)
					: 0;
				var ms = match.Groups["ms"].Success
					? float.Parse($"0.{match.Groups["ms"].Value}", CultureInfo.InvariantCulture)
					: 0f;

				if (h < 24 && m < 60 && s < 60)
				{
					var totalSeconds = h * SecondsPerHour + m * SecondsPerMinute + s + ms;
					return totalSeconds.ToString(CultureInfo.InvariantCulture);
				}
			}
		}

		return text;
	}

	private string AppendUnits(string formatted)
	{
		if (string.IsNullOrEmpty(_units))
		{
			return formatted;
		}

		return $"{formatted} {_units}";
	}

	private string StripUnitsSuffix(string text)
	{
		if (string.IsNullOrEmpty(_units))
		{
			return text;
		}

		if (text.EndsWith(_units, StringComparison.Ordinal))
		{
			return text[..^_units.Length].Trim();
		}

		return text;
	}

	[GeneratedRegex(@"^(?<h>\d{1,2}):(?<m>\d{1,2})(:(?<s>\d{1,2})(\.(?<ms>\d+))?)?$", RegexOptions.Compiled)]
	private static partial Regex TimePattern();
}
