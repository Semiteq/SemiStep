using System.Globalization;

using Avalonia.Controls;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

using SemiStep.UI.RecipeGrid.Transposed;

namespace SemiStep.UI.RecipeGrid;

public sealed class ColumnWidthCalculator(
	RecipeMetadataRegistry recipeMetadataRegistry,
	GridStyleOptions gridStyle)
{
	// Every hh:mm:ss start time under 100 hours renders at the width of this sample.
	private const string RepresentativeTimeRawSeconds = "0";

	// String MaxLength can be very large (production props use 64/255); cap the representative sample
	// so a wide string column does not dominate the available FullHD width.
	private const int MaxStringSampleLength = 12;

	// ceil(CellFontSize × 2.0); calibration of the prior 26 px. See docs/architecture/recipe-grid-column-sizing.md
	private const double ChromeFontMultiple = 2.0;

	// em multiple calibrated to reproduce 72 at the default font. See docs/architecture/recipe-grid-column-sizing.md
	private const double MinColumnWidthEms = 6.0;

	private const double ComboBoxChromeWidth =
		ComboBoxCellFactory.DisplayPaddingLeft + ComboBoxCellFactory.ChevronSlotWidth;

	// Designer cap for the transposed step column; a longer action name trims with a tooltip.
	private const double TransposedStepColumnCap = 240;

	private readonly GridTextMeasurer _textMeasurer = new(gridStyle);
	private double? _widestUnitSlotWidth;

	// See docs/architecture/recipe-grid-column-sizing.md
	private int ContentChrome => (int)Math.Ceiling(gridStyle.Fonts.CellFontSize * ChromeFontMultiple);

	// The transposed name display pads on both sides, and its cell border takes one more grid line.
	private double TransposedComboChromeWidth =>
		gridStyle.Layout.CellPaddingLeft
		+ gridStyle.Layout.CellPaddingRight
		+ ComboBoxCellFactory.ChevronSlotWidth
		+ TransposedColumnCellsPresenter.GridLineWidth;

	private int HeaderFloorChrome => (int)Math.Ceiling(gridStyle.Fonts.HeaderFontSize * ChromeFontMultiple);

	// The numbering column (ColumnBuilder) uses it as its MinWidth, so do not lower it.
	public int MinColumnWidth => (int)Math.Ceiling(gridStyle.Fonts.CellFontSize * MinColumnWidthEms);

	/// <summary>A property column whose type is int or float, or the step start time column.</summary>
	public bool IsNumericColumn(GridColumnDefinition columnDef)
	{
		if (ColumnTypes.IsStepStartTimeColumn(columnDef.ColumnType))
		{
			return true;
		}

		return string.Equals(columnDef.ColumnType, ColumnTypes.PropertyField, StringComparison.OrdinalIgnoreCase)
			&& recipeMetadataRegistry.GetProperty(columnDef.PropertyTypeId) is { IsSuccess: true } property
			&& (SystemTypes.Comparer.Equals(property.Value.SystemType, SystemTypes.Int)
				|| SystemTypes.Comparer.Equals(property.Value.SystemType, SystemTypes.Float));
	}

	public IReadOnlySet<string> NumericColumnKeys()
	{
		return recipeMetadataRegistry.GetAllColumns()
			.Where(IsNumericColumn)
			.Select(column => column.Key)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>The widest unit any action shows in the column plus the gap; time_hms units do not count.</summary>
	public double UnitSlotWidth(string columnKey)
	{
		var columnPropertyTypeId = recipeMetadataRegistry.GetColumn(columnKey) is { IsSuccess: true } column
			? column.Value.PropertyTypeId
			: null;
		var widest = ColumnPropertyTypes(columnKey, columnPropertyTypeId)
			.Where(property => !IsTimeFormat(property) && !string.IsNullOrEmpty(property.Units))
			.Select(property => _textMeasurer.MeasureContent(property.Units!))
			.DefaultIfEmpty(0)
			.Max();

		return widest > 0 ? Math.Ceiling(widest + TextCellLayout.UnitGap) : 0;
	}

	/// <summary>The transposed grid shares one step column width, so it shares one unit slot too.</summary>
	public double WidestUnitSlotWidth()
	{
		return _widestUnitSlotWidth ??= NumericColumnKeys().Select(UnitSlotWidth).DefaultIfEmpty(0).Max();
	}

	/// <summary>
	/// One width for every transposed step column: the wider of the longest action name with the combo chrome and
	/// the widest value with the unit slot, capped at <see cref="TransposedStepColumnCap"/>.
	/// </summary>
	public double TransposedStepColumnWidth()
	{
		var widestName = recipeMetadataRegistry.GetAllActions()
			.Select(action => _textMeasurer.MeasureContent(action.UiName))
			.DefaultIfEmpty(0)
			.Max();
		var widestValue = recipeMetadataRegistry.GetAllColumns()
			.Where(column => string.Equals(
				column.ColumnType,
				ColumnTypes.PropertyField,
				StringComparison.OrdinalIgnoreCase))
			.SelectMany(column => ColumnPropertyTypes(column.Key, column.PropertyTypeId))
			.SelectMany(PropertyRepresentatives)
			.Select(_textMeasurer.MeasureContent)
			.DefaultIfEmpty(0)
			.Max();
		var width = Math.Max(
			widestName + TransposedComboChromeWidth,
			widestValue + WidestUnitSlotWidth() + ContentChrome);

		return Math.Min(TransposedStepColumnCap, Math.Ceiling(Math.Max(width, MinColumnWidth)));
	}

	public DataGridLength CalculateColumnWidth(GridColumnDefinition columnDef)
	{
		return ColumnTypes.IsGroupBoundColumn(columnDef.ColumnType)
			? CalculateGroupColumnWidth(columnDef)
			: columnDef.ColumnType.ToLowerInvariant() switch
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
			units: null);

		return CalculateWidth([representative], columnDef.UiName);
	}

	// A number column measures the value and its unit slot apart, as the cell lays them out.
	private DataGridLength CalculatePropertyFieldWidth(GridColumnDefinition columnDef)
	{
		var representatives = ColumnPropertyTypes(columnDef.Key, columnDef.PropertyTypeId)
			.SelectMany(PropertyRepresentatives);
		var unitSlot = IsNumericColumn(columnDef) ? UnitSlotWidth(columnDef.Key) : 0;

		return CalculateWidth(representatives, columnDef.UiName, ContentChrome + unitSlot);
	}

	private IEnumerable<PropertyTypeDefinition> ColumnPropertyTypes(
		string columnKey,
		string? columnPropertyTypeId = null)
	{
		var propertyTypeIds = recipeMetadataRegistry.GetAllActions()
			.SelectMany(action => action.Properties)
			.Where(property => property.Key == columnKey)
			.Select(property => property.PropertyTypeId);
		if (columnPropertyTypeId is not null)
		{
			propertyTypeIds = propertyTypeIds.Append(columnPropertyTypeId);
		}

		return propertyTypeIds
			.Distinct()
			.Select(recipeMetadataRegistry.GetProperty)
			.Where(result => result.IsSuccess)
			.Select(result => result.Value);
	}

	private static bool IsTimeFormat(PropertyTypeDefinition property)
	{
		return string.Equals(property.FormatKind, TimeFormatHelper.TimeHmsFormat, StringComparison.OrdinalIgnoreCase);
	}

	private static IEnumerable<string> PropertyRepresentatives(PropertyTypeDefinition propertyDef)
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

		return TimeFormatHelper.FormatValue(rawValue, propertyDef.FormatKind, units: null);
	}

	private DataGridLength CalculateWidth(
		IEnumerable<string> contentStrings,
		string headerText,
		double? chromeOverride = null)
	{
		var maxContentWidth = 0.0;
		foreach (var text in contentStrings)
		{
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}

			var contentWidth = _textMeasurer.MeasureContent(text);
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
			var wordWidth = _textMeasurer.MeasureHeaderWord(word);
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

			foreach (var item in groupResult.Value.Items.Values)
			{
				yield return item;
			}
		}
	}
}
