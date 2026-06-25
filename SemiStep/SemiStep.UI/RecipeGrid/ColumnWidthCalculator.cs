using System.Globalization;

using Avalonia.Controls;
using Avalonia.Media;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

public sealed class ColumnWidthCalculator(
	RecipeMetadataRegistry recipeMetadataRegistry,
	GridStyleOptions gridStyle)
{
	// Raw seconds whose units-bearing render ("00:00:00 с") is the widest step-start-time cell value.
	private const string RepresentativeTimeRawSeconds = "0";

	// String MaxLength can be very large (production props use 64/255); cap the representative sample
	// so a wide string column does not dominate the available FullHD width.
	private const int MaxStringSampleLength = 12;

	// ceil(CellFontSize × 2.0); calibration of the prior 26 px. See Docs/architecture/recipe-grid-column-sizing.md
	private const double ChromeFontMultiple = 2.0;

	// em multiple calibrated to reproduce 72 at the default font. See Docs/architecture/recipe-grid-column-sizing.md
	private const double MinColumnWidthEms = 6.0;

	// 32 chevron column + 6 left padding; fixed theme DIP. See Docs/architecture/recipe-grid-column-sizing.md
	private const int ComboBoxChromeWidth = 38;

	// See Docs/architecture/recipe-grid-column-sizing.md
	private int ContentChrome => (int)Math.Ceiling(gridStyle.CellFontSize * ChromeFontMultiple);

	private int HeaderFloorChrome => (int)Math.Ceiling(gridStyle.HeaderFontSize * ChromeFontMultiple);

	// The numbering column (ColumnBuilder) uses it as its MinWidth, so do not lower it.
	public int MinColumnWidth => (int)Math.Ceiling(gridStyle.CellFontSize * MinColumnWidthEms);

	public DataGridLength CalculateColumnWidth(GridColumnDefinition columnDef)
	{
		if (ColumnTypes.IsGroupBoundColumn(columnDef.ColumnType))
		{
			return CalculateGroupColumnWidth(columnDef);
		}

		return columnDef.ColumnType.ToLowerInvariant() switch
		{
			ColumnTypes.ActionComboBox => CalculateActionColumnWidth(columnDef),
			ColumnTypes.PropertyField => CalculatePropertyFieldWidth(columnDef),
			ColumnTypes.StepStartTimeField => CalculateTimeColumnWidth(columnDef),
			ColumnTypes.TextField => new DataGridLength(1, DataGridLengthUnitType.Star),
			_ => CalculateWidth([], columnDef.UiName)
		};
	}

	private DataGridLength CalculateActionColumnWidth(GridColumnDefinition columnDef)
	{
		var actionNames = recipeMetadataRegistry.GetAllActions().Select(a => a.UiName);

		return CalculateWidth(actionNames, columnDef.UiName, ComboBoxChromeWidth);
	}

	private DataGridLength CalculateGroupColumnWidth(GridColumnDefinition columnDef)
	{
		var displayStrings = CollectGroupDisplayStrings(columnDef.Key);

		return CalculateWidth(displayStrings, columnDef.UiName, ComboBoxChromeWidth);
	}

	private DataGridLength CalculateTimeColumnWidth(GridColumnDefinition columnDef)
	{
		var representative = TimeFormatHelper.FormatValue(
			RepresentativeTimeRawSeconds,
			TimeFormatHelper.TimeHmsFormat,
			TimeFormatHelper.TimeUnits);

		return CalculateWidth([representative], columnDef.UiName);
	}

	private DataGridLength CalculatePropertyFieldWidth(GridColumnDefinition columnDef)
	{
		var representatives = recipeMetadataRegistry.GetAllActions()
			.SelectMany(action => action.Properties)
			.Where(property => property.Key == columnDef.Key)
			.Select(property => property.PropertyTypeId)
			.Append(columnDef.PropertyTypeId)
			.Distinct()
			.Select(recipeMetadataRegistry.GetProperty)
			.Where(result => result.IsSuccess)
			.SelectMany(result => PropertyRepresentatives(result.Value));

		return CalculateWidth(representatives, columnDef.UiName);
	}

	private IEnumerable<string> PropertyRepresentatives(PropertyTypeDefinition propertyDef)
	{
		if (SystemTypes.Comparer.Equals(propertyDef.SystemType, SystemTypes.String))
		{
			var sampleLength = Math.Min(propertyDef.MaxLength ?? MaxStringSampleLength, MaxStringSampleLength);
			yield return new string('0', sampleLength);

			yield break;
		}

		if (propertyDef.Max is not null)
		{
			yield return FormatExtent(propertyDef.Max.Value, propertyDef);
		}

		if (propertyDef.Min is not null)
		{
			yield return FormatExtent(propertyDef.Min.Value, propertyDef);
		}
	}

	private static string FormatExtent(double extent, PropertyTypeDefinition propertyDef)
	{
		var rawValue = extent.ToString(CultureInfo.InvariantCulture);

		return TimeFormatHelper.FormatValue(rawValue, propertyDef.FormatKind, propertyDef.Units);
	}

	private DataGridLength CalculateWidth(IEnumerable<string> contentStrings, string headerText, double? chromeOverride = null)
	{
		var maxContentWidth = 0.0;
		foreach (var text in contentStrings)
		{
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}

			var contentWidth = MeasureText(text, gridStyle.CellFontSize, FontWeight.Normal);
			if (contentWidth > maxContentWidth)
			{
				maxContentWidth = contentWidth;
			}
		}

		var contentBudget = maxContentWidth + (chromeOverride ?? ContentChrome);
		var pixelWidth = (int)Math.Ceiling(
			Math.Max(Math.Max(contentBudget, LongestHeaderWordFloor(headerText)), MinColumnWidth));

		return new DataGridLength(pixelWidth);
	}

	private double LongestHeaderWordFloor(string header)
	{
		if (string.IsNullOrWhiteSpace(header))
		{
			return 0;
		}

		var words = header.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

		var maxWordWidth = 0.0;
		foreach (var word in words)
		{
			var wordWidth = MeasureText(word, gridStyle.HeaderFontSize, FontWeight.Bold);
			if (wordWidth > maxWordWidth)
			{
				maxWordWidth = wordWidth;
			}
		}

		return maxWordWidth + HeaderFloorChrome;
	}

	private IEnumerable<string> CollectGroupDisplayStrings(string columnKey)
	{
		var groupNames = new HashSet<string>();

		foreach (var action in recipeMetadataRegistry.GetAllActions())
		{
			var actionColumn = action.Properties.FirstOrDefault(c => c.Key == columnKey);
			if (actionColumn?.GroupName is not null)
			{
				groupNames.Add(actionColumn.GroupName);
			}
		}

		foreach (var groupName in groupNames)
		{
			var groupResult = recipeMetadataRegistry.GetGroup(groupName);
			if (groupResult.IsFailed)
			{
				continue;
			}

			var group = groupResult.Value;
			foreach (var item in group.Items.Values)
			{
				yield return item;
			}
		}
	}

	private static double MeasureText(string text, double fontSize, FontWeight fontWeight)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}

		var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, fontWeight);
		var formattedText = new FormattedText(
			text,
			CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			typeface,
			fontSize,
			Brushes.Black);

		return formattedText.WidthIncludingTrailingWhitespace;
	}
}
