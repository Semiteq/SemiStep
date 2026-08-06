using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Mapping;

internal static class GridStyleMapper
{
	public static Result<GridStyleOptions> Map(GridStyleOptionsDto? dto)
	{
		if (dto is null)
		{
			return Result.Fail(new GridStyleConfigMissingError());
		}

		if (dto.Colors is null)
		{
			return Result.Fail(new GridStyleSectionMissingError("colors"));
		}

		var defaults = GridStyleOptions.Default;
		var errors = new List<IError>();

		// Parse phase in the validator's emission order: execution -> readonly -> disabled -> chrome ->
		// selection -> cells.changed/changed_selected -> colors.grid_line -> status_bar -> validation_panel ->
		// orientation. The join into one user-visible message depends on this order.
		var execution = MapExecution(dto.Colors.Cells?.Execution, defaults.Execution, errors);
		var readOnly = MapReadOnly(dto.Colors.Cells?.ReadOnly, defaults.ReadOnlyCells, errors);
		var disabled = MapDisabled(dto.Colors.Cells?.Disabled, defaults.DisabledCells, errors);
		var chrome = MapChrome(dto.Chrome, defaults.Chrome, errors);
		var selection = MapSelection(dto.Colors.Selection, defaults.Selection, errors);
		var changedCells = MapChangedCells(dto.Colors.Cells, defaults.ChangedCells, errors);
		var gridLine = OptionalColor(dto.Colors.GridLine, defaults.Chrome.GridLine, "colors", "grid_line", errors);
		chrome = chrome with { GridLine = gridLine };
		var statusBar = MapStatusBar(dto.StatusBar, defaults.StatusBar, errors);
		var validationPanel = MapValidationPanel(dto.ValidationPanel, defaults.ValidationPanel, errors);
		var orientation = MapOrientation(dto.Orientation, errors);

		if (errors.Count > 0)
		{
			return Result.Fail(errors);
		}

		return Result.Ok(new GridStyleOptions(
			Fonts: new GridStyleFonts(
				FontFamily: dto.Fonts?.Family ?? defaults.Fonts.FontFamily,
				HeaderFontSize: dto.Fonts?.HeaderSize ?? defaults.Fonts.HeaderFontSize,
				HeaderFontWeight: dto.Fonts?.HeaderWeight ?? defaults.Fonts.HeaderFontWeight,
				HeaderItalic: dto.Fonts?.HeaderItalic ?? defaults.Fonts.HeaderItalic,
				CellFontSize: dto.Fonts?.CellSize ?? defaults.Fonts.CellFontSize,
				CellFontWeight: dto.Fonts?.CellWeight ?? defaults.Fonts.CellFontWeight,
				CellItalic: dto.Fonts?.CellItalic ?? defaults.Fonts.CellItalic),
			Layout: new GridStyleLayout(
				CellPaddingLeft: dto.Layout?.CellPaddingLeft ?? defaults.Layout.CellPaddingLeft,
				CellPaddingTop: dto.Layout?.CellPaddingTop ?? defaults.Layout.CellPaddingTop,
				CellPaddingRight: dto.Layout?.CellPaddingRight ?? defaults.Layout.CellPaddingRight,
				CellPaddingBottom: dto.Layout?.CellPaddingBottom ?? defaults.Layout.CellPaddingBottom,
				RowHeight: dto.Layout?.RowHeight ?? defaults.Layout.RowHeight),
			Selection: selection,
			ChangedCells: changedCells,
			ReadOnlyCells: readOnly,
			DisabledCells: disabled,
			Execution: execution,
			StatusBar: statusBar,
			ValidationPanel: validationPanel,
			Chrome: chrome,
			Orientation: orientation));
	}

	private static ExecutionPalette MapExecution(
		GridStyleExecutionColorsDto? dto,
		ExecutionPalette defaults,
		List<IError> errors)
	{
		var section = "colors.cells.execution";
		if (dto is null)
		{
			errors.Add(new GridStyleSectionMissingError(section));
			return defaults;
		}

		return new ExecutionPalette(
			Depth0: RequiredColor(dto.Depth0, section, "depth_0", errors),
			Depth1: RequiredColor(dto.Depth1, section, "depth_1", errors),
			Depth2: RequiredColor(dto.Depth2, section, "depth_2", errors),
			Depth3: RequiredColor(dto.Depth3, section, "depth_3", errors),
			Depth0Past: RequiredColor(dto.Depth0Past, section, "depth_0_past", errors),
			Depth1Past: RequiredColor(dto.Depth1Past, section, "depth_1_past", errors),
			Depth2Past: RequiredColor(dto.Depth2Past, section, "depth_2_past", errors),
			Depth3Past: RequiredColor(dto.Depth3Past, section, "depth_3_past", errors),
			CurrentStepMarker: RequiredColor(dto.CurrentStepMarker, section, "current_step_marker", errors));
	}

	private static DepthPalette MapReadOnly(
		GridStyleReadOnlyCellColorsDto? dto,
		DepthPalette defaults,
		List<IError> errors)
	{
		var section = "colors.cells.readonly";
		if (dto is null)
		{
			errors.Add(new GridStyleSectionMissingError(section));
			return defaults;
		}

		return new DepthPalette(
			Depth0: RequiredColor(dto.Depth0, section, "depth_0", errors),
			Depth1: RequiredColor(dto.Depth1, section, "depth_1", errors),
			Depth2: RequiredColor(dto.Depth2, section, "depth_2", errors),
			Depth3: RequiredColor(dto.Depth3, section, "depth_3", errors),
			Depth0Past: RequiredColor(dto.Depth0Past, section, "depth_0_past", errors),
			Depth1Past: RequiredColor(dto.Depth1Past, section, "depth_1_past", errors),
			Depth2Past: RequiredColor(dto.Depth2Past, section, "depth_2_past", errors),
			Depth3Past: RequiredColor(dto.Depth3Past, section, "depth_3_past", errors),
			Selected: RequiredColor(dto.Selected, section, "selected", errors),
			Foreground: RequiredColor(dto.Foreground, section, "foreground", errors));
	}

	private static DepthPalette MapDisabled(
		GridStyleDisabledCellColorsDto? dto,
		DepthPalette defaults,
		List<IError> errors)
	{
		var section = "colors.cells.disabled";
		if (dto is null)
		{
			errors.Add(new GridStyleSectionMissingError(section));
			return defaults;
		}

		return new DepthPalette(
			Depth0: RequiredColor(dto.Depth0, section, "depth_0", errors),
			Depth1: RequiredColor(dto.Depth1, section, "depth_1", errors),
			Depth2: RequiredColor(dto.Depth2, section, "depth_2", errors),
			Depth3: RequiredColor(dto.Depth3, section, "depth_3", errors),
			Depth0Past: RequiredColor(dto.Depth0Past, section, "depth_0_past", errors),
			Depth1Past: RequiredColor(dto.Depth1Past, section, "depth_1_past", errors),
			Depth2Past: RequiredColor(dto.Depth2Past, section, "depth_2_past", errors),
			Depth3Past: RequiredColor(dto.Depth3Past, section, "depth_3_past", errors),
			Selected: RequiredColor(dto.Selected, section, "selected", errors),
			Foreground: RequiredColor(dto.Foreground, section, "foreground", errors));
	}

	private static ChromeColors MapChrome(
		GridStyleChromeColorsDto? dto,
		ChromeColors defaults,
		List<IError> errors)
	{
		var section = "chrome";
		return new ChromeColors(
			Info: OptionalColor(dto?.Info, defaults.Info, section, "info", errors),
			Connected: OptionalColor(dto?.Connected, defaults.Connected, section, "connected", errors),
			Disconnected: OptionalColor(dto?.Disconnected, defaults.Disconnected, section, "disconnected", errors),
			LocalMode: OptionalColor(dto?.LocalMode, defaults.LocalMode, section, "local_mode", errors),
			Connecting: OptionalColor(dto?.Connecting, defaults.Connecting, section, "connecting", errors),
			PanelBackground: OptionalColor(
				dto?.PanelBackground, defaults.PanelBackground, section, "panel_background", errors),
			PanelHeaderBackground: OptionalColor(
				dto?.PanelHeaderBackground, defaults.PanelHeaderBackground, section, "panel_header_background", errors),
			SubtleBorder: OptionalColor(dto?.SubtleBorder, defaults.SubtleBorder, section, "subtle_border", errors),
			Separator: OptionalColor(dto?.Separator, defaults.Separator, section, "separator", errors),
			SecondaryForeground: OptionalColor(
				dto?.SecondaryForeground, defaults.SecondaryForeground, section, "secondary_foreground", errors),
			GridBorder: OptionalColor(dto?.GridBorder, defaults.GridBorder, section, "grid_border", errors),
			GridBackground: OptionalColor(
				dto?.GridBackground, defaults.GridBackground, section, "grid_background", errors),
			HeaderForeground: OptionalColor(
				dto?.HeaderForeground, defaults.HeaderForeground, section, "header_foreground", errors),
			// Placeholder: Map overwrites this with the parsed colors.grid_line so that key's error lands
			// after changed_selected in emission order, not here in the chrome block.
			GridLine: defaults.GridLine);
	}

	private static SelectionColors MapSelection(
		GridStyleSelectionColorsDto? dto,
		SelectionColors defaults,
		List<IError> errors)
	{
		var section = "colors.selection";
		return new SelectionColors(
			Background: OptionalColor(dto?.Background, defaults.Background, section, "background", errors),
			Foreground: OptionalColor(dto?.Foreground, defaults.Foreground, section, "foreground", errors));
	}

	private static ChangedCellColors MapChangedCells(
		GridStyleCellColorsDto? cells,
		ChangedCellColors defaults,
		List<IError> errors)
	{
		var section = "colors.cells";
		return new ChangedCellColors(
			Changed: OptionalColor(cells?.Changed, defaults.Changed, section, "changed", errors),
			ChangedSelected: OptionalColor(
				cells?.ChangedSelected, defaults.ChangedSelected, section, "changed_selected", errors));
	}

	private static StatusBarStyle MapStatusBar(
		StatusBarStyleDto? dto,
		StatusBarStyle defaults,
		List<IError> errors)
	{
		var section = "status_bar";
		return new StatusBarStyle(
			Background: OptionalColor(dto?.Background, defaults.Background, section, "background", errors),
			Foreground: OptionalColor(dto?.Foreground, defaults.Foreground, section, "foreground", errors),
			Padding: dto?.Padding ?? defaults.Padding,
			ItemSpacing: dto?.ItemSpacing ?? defaults.ItemSpacing,
			FontSize: dto?.FontSize ?? defaults.FontSize,
			Weight: dto?.Weight ?? defaults.Weight,
			Italic: dto?.Italic ?? defaults.Italic,
			TimerLabelFontSize: dto?.TimerLabelFontSize ?? defaults.TimerLabelFontSize,
			TimerLabelWeight: dto?.TimerLabelWeight ?? defaults.TimerLabelWeight,
			TimerLabelItalic: dto?.TimerLabelItalic ?? defaults.TimerLabelItalic,
			TimerValueFontSize: dto?.TimerValueFontSize ?? defaults.TimerValueFontSize,
			TimerValueWeight: dto?.TimerValueWeight ?? defaults.TimerValueWeight,
			TimerValueItalic: dto?.TimerValueItalic ?? defaults.TimerValueItalic);
	}

	private static ValidationPanelStyle MapValidationPanel(
		ValidationPanelStyleDto? dto,
		ValidationPanelStyle defaults,
		List<IError> errors)
	{
		var section = "validation_panel";
		return new ValidationPanelStyle(
			Background: OptionalColor(dto?.Background, defaults.Background, section, "background", errors),
			Foreground: OptionalColor(dto?.Foreground, defaults.Foreground, section, "foreground", errors),
			ErrorColor: OptionalColor(dto?.ErrorColor, defaults.ErrorColor, section, "error_color", errors),
			WarningColor: OptionalColor(dto?.WarningColor, defaults.WarningColor, section, "warning_color", errors),
			MaxHeight: dto?.MaxHeight ?? defaults.MaxHeight);
	}

	private static GridOrientation MapOrientation(string? orientation, List<IError> errors)
	{
		if (orientation is not null
			&& orientation != GridOrientationValues.RowsAsSteps
			&& orientation != GridOrientationValues.ColumnsAsSteps)
		{
			errors.Add(new GridStyleOrientationInvalidError(
				orientation,
				GridOrientationValues.RowsAsSteps,
				GridOrientationValues.ColumnsAsSteps));
			return GridOrientation.RowsAsSteps;
		}

		return GridOrientationValues.Parse(orientation);
	}

	// The placeholder returned on error is discarded once `errors` is non-empty.
	private static StyleColor RequiredColor(string? value, string section, string key, List<IError> errors)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			errors.Add(new GridStyleKeyMissingError(section, key));
			return default;
		}

		if (!StyleColor.TryParse(value, out var color))
		{
			errors.Add(new GridStyleHexColorInvalidError(section, key, value));
			return default;
		}

		return color;
	}

	// Optional key: null falls back silently; present-but-whitespace is a
	// missing key; a non-parsing value is an invalid hex.
	private static StyleColor OptionalColor(
		string? value,
		StyleColor fallback,
		string section,
		string key,
		List<IError> errors)
	{
		if (value is null)
		{
			return fallback;
		}

		if (string.IsNullOrWhiteSpace(value))
		{
			errors.Add(new GridStyleKeyMissingError(section, key));
			return fallback;
		}

		if (!StyleColor.TryParse(value, out var color))
		{
			errors.Add(new GridStyleHexColorInvalidError(section, key, value));
			return fallback;
		}

		return color;
	}
}
