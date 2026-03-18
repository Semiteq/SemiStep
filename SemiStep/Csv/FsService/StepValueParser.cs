using System.Globalization;

using Csv.Helpers;

using FluentResults;

using Shared.Config;
using Shared.Core;

namespace Csv.FsService;

internal static class StepValueParser
{
	internal static string FormatStepValue(Step step, GridColumnDefinition column)
	{
		if (column.Key == CsvStepWriter.ActionColumnKey)
		{
			return step.ActionKey.ToString(CultureInfo.InvariantCulture);
		}

		var columnId = new ColumnId(column.Key);
		if (!step.Properties.TryGetValue(columnId, out var propertyValue))
		{
			return string.Empty;
		}

		return propertyValue.Type switch
		{
			PropertyType.Int => propertyValue.AsInt().ToString(CultureInfo.InvariantCulture),
			PropertyType.Float => propertyValue.AsFloat().ToString("R", CultureInfo.InvariantCulture),
			PropertyType.String => propertyValue.AsString(),
			_ => propertyValue.Value?.ToString() ?? string.Empty
		};
	}

	internal static Result<PropertyValue> TryParsePropertyValue(
		string rawValue,
		PropertyDefinition propertyDef,
		string columnKey,
		int rowNumber)
	{
		return propertyDef.SystemType.ToLowerInvariant() switch
		{
			"int" => TryParseIntProperty(rawValue, columnKey, rowNumber),
			"float" => TryParseFloatProperty(rawValue, columnKey, rowNumber),
			"string" => Result.Ok(PropertyValue.FromString(rawValue)),
			_ => Result.Fail($"Row {rowNumber}: unknown system type '{propertyDef.SystemType}' for column '{columnKey}'")
		};
	}

	private static Result<PropertyValue> TryParseIntProperty(
		string rawValue, string columnKey, int rowNumber)
	{
		if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
		{
			return Result.Fail(
				$"Row {rowNumber}: cannot parse value '{rawValue}' as int for column '{columnKey}'");
		}

		return PropertyValue.FromInt(intValue);
	}

	private static Result<PropertyValue> TryParseFloatProperty(
		string rawValue, string columnKey, int rowNumber)
	{
		if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
		{
			return Result.Fail(
				$"Row {rowNumber}: cannot parse value '{rawValue}' as float for column '{columnKey}'");
		}

		return PropertyValue.FromFloat(floatValue);
	}
}
