using System.Globalization;

using TypesShared.Config;

namespace TypesShared.Core;

public static class StepValueParser
{
	public const string ActionColumnKey = "action";

	public static string FormatStepValue(Step step, GridColumnDefinition column)
	{
		if (column.Key == ActionColumnKey)
		{
			return step.ActionKey.ToString(CultureInfo.InvariantCulture);
		}

		var columnId = new PropertyId(column.Key);
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
}
