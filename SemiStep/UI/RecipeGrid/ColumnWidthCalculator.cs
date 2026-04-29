using System.Globalization;

using Avalonia.Controls;
using Avalonia.Media;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

namespace UI.RecipeGrid;

public sealed class ColumnWidthCalculator(
	RecipeMetadataRegistry configRegistry,
	GridStyleOptions gridStyle)
{
	private const string RepresentativeTimeValue = "00:00:00";
	private const double BufferMultiplier = 1.4;

	public DataGridLength CalculateColumnWidth(GridColumnDefinition columnDef)
	{
		return columnDef.ColumnType.ToLowerInvariant() switch
		{
			ColumnTypes.ActionComboBox => CalculateActionColumnWidth(columnDef),
			ColumnTypes.ActionTargetComboBox => CalculateGroupColumnWidth(columnDef),
			ColumnTypes.PropertyField => CalculateHeaderBasedWidth(columnDef),
			ColumnTypes.StepStartTimeField => CalculateTimeColumnWidth(columnDef),
			ColumnTypes.TextField => new DataGridLength(1, DataGridLengthUnitType.Star),
			_ => CalculateHeaderBasedWidth(columnDef)
		};
	}

	private DataGridLength CalculateActionColumnWidth(GridColumnDefinition columnDef)
	{
		var actionNames = configRegistry.GetAllActions().Select(a => a.UiName);

		return CalculateWidth(columnDef.UiName, actionNames);
	}

	private DataGridLength CalculateGroupColumnWidth(GridColumnDefinition columnDef)
	{
		var displayStrings = CollectGroupDisplayStrings(columnDef.Key);

		return CalculateWidth(columnDef.UiName, displayStrings);
	}

	private DataGridLength CalculateHeaderBasedWidth(GridColumnDefinition columnDef)
	{
		return CalculateWidth(columnDef.UiName, []);
	}

	private DataGridLength CalculateTimeColumnWidth(GridColumnDefinition columnDef)
	{
		return CalculateWidth(columnDef.UiName, [RepresentativeTimeValue]);
	}

	private DataGridLength CalculateWidth(string headerText, IEnumerable<string> contentStrings)
	{
		var headerWidth = CompensateThemeSortIconAndPaddingOffset(headerText);

		var maxContentWidth = 0.0;
		foreach (var text in contentStrings)
		{
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}

			var contentWidth = MeasureText(text, gridStyle.CellFontSize);
			if (contentWidth > maxContentWidth)
			{
				maxContentWidth = contentWidth;
			}
		}

		var maxWidth = Math.Max(headerWidth, maxContentWidth);
		var pixelWidth = (int)Math.Ceiling(maxWidth * BufferMultiplier);

		return new DataGridLength(pixelWidth);
	}

	private IEnumerable<string> CollectGroupDisplayStrings(string columnKey)
	{
		var groupNames = new HashSet<string>();

		foreach (var action in configRegistry.GetAllActions())
		{
			var actionColumn = action.Properties.FirstOrDefault(c => c.Key == columnKey);
			if (actionColumn?.GroupName is not null)
			{
				groupNames.Add(actionColumn.GroupName);
			}
		}

		foreach (var groupName in groupNames)
		{
			var groupResult = configRegistry.GetGroup(groupName);
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

	private double CompensateThemeSortIconAndPaddingOffset(string headerText)
	{
		const double FluentThemeSortIconMinWidth = 32;

		var textWidth = MeasureText(headerText, gridStyle.HeaderFontSize);

		return textWidth + FluentThemeSortIconMinWidth;
	}

	private static double MeasureText(string text, double fontSize)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}

		var typeface = new Typeface(FontFamily.Default);
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
