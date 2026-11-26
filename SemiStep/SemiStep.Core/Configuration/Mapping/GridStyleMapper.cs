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

		// Parse phase in the validator's emission order: execution -> loop -> readonly -> disabled -> chrome ->
		// selection -> cells.changed -> colors.grid_line -> status_bar -> validation_panel -> orientation.
		// The join into one user-visible message depends on this order.
		var execution = MapExecution(dto.Colors.Cells?.Execution, defaults.Execution, errors);
		var loop = MapLoop(dto.Colors.Cells?.Loop, defaults.Loop, errors);
		var readOnly = MapGreyCell(dto.Colors.Cells?.ReadOnly, "colors.cells.readonly", errors);
		var disabled = MapGreyCell(dto.Colors.Cells?.Disabled, "colors.cells.disabled", errors);
		var chrome = MapChrome(dto.Chrome, defaults.Chrome, errors);
		var selection = MapSelection(dto.Colors.Selection, defaults.Selection, errors);
		var changedCells = MapChangedCells(dto.Colors.Cells, defaults.ChangedCells, errors);
		var gridLine = OptionalColor(dto.Colors.GridLine, defaults.Chrome.GridLine, "colors", "grid_line", errors);
		chrome = chrome with { GridLine = gridLine };
		var statusBar = MapStatusBar(dto.StatusBar, defaults.StatusBar, errors);
		var validationPanel = MapValidationPanel(dto.ValidationPanel, defaults.ValidationPanel, errors);
		var orientation = MapOrientation(dto.Orientation, errors);

		return errors.Count > 0
			? (Result<GridStyleOptions>)Result.Fail(errors)
			: Result.Ok(new GridStyleOptions(
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
			ReadOnlyCell: readOnly,
			DisabledCell: disabled,
			Execution: execution,
			Loop: loop,
			StatusBar: statusBar,
			ValidationPanel: validationPanel,
			Chrome: chrome,
			Orientation: orientation));
	}

	// The section and every key in it are optional; a missing one takes the default.
	private static ExecutionPalette MapExecution(
		GridStyleExecutionColorsDto? dto,
		ExecutionPalette defaults,
		List<IError> errors)
	{
		var section = "colors.cells.execution";
		return new ExecutionPalette(
			Running: OptionalColor(dto?.Running, defaults.Running, section, "running", errors),
			RunningWeight: OptionalWeight(dto?.RunningWeight, defaults.RunningWeight, section, "running_weight", errors),
			PastWeight: OptionalWeight(dto?.PastWeight, defaults.PastWeight, section, "past_weight", errors));
	}

	private static LoopPalette MapLoop(
		GridStyleLoopColorsDto? dto,
		LoopPalette defaults,
		List<IError> errors)
	{
		var section = "colors.cells.loop";
		return new LoopPalette(
			Color: OptionalColor(dto?.Color, defaults.Color, section, "color", errors),
			Depth1Weight: OptionalWeight(dto?.Depth1Weight, defaults.Depth1Weight, section, "depth_1_weight", errors),
			Depth2Weight: OptionalWeight(dto?.Depth2Weight, defaults.Depth2Weight, section, "depth_2_weight", errors),
			Depth3Weight: OptionalWeight(dto?.Depth3Weight, defaults.Depth3Weight, section, "depth_3_weight", errors));
	}

	private static StyleColor MapGreyCell(GridStyleGreyCellColorsDto? dto, string section, List<IError> errors)
	{
		if (dto is null)
		{
			errors.Add(new GridStyleSectionMissingError(section));
			return default;
		}

		return RequiredColor(dto.Depth0, section, "depth_0", errors);
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
			// after cells.changed in emission order, not here in the chrome block.
			GridLine: defaults.GridLine);
	}

	private static SelectionColors MapSelection(
		GridStyleSelectionColorsDto? dto,
		SelectionColors defaults,
		List<IError> errors)
	{
		var section = "colors.selection";
		return new SelectionColors(
			Accent: OptionalColor(dto?.Accent, defaults.Accent, section, "accent", errors),
			Weight: OptionalWeight(dto?.Weight, defaults.Weight, section, "weight", errors));
	}

	private static ChangedCellColors MapChangedCells(
		GridStyleCellColorsDto? cells,
		ChangedCellColors defaults,
		List<IError> errors)
	{
		var section = "colors.cells";
		return new ChangedCellColors(
			Changed: OptionalColor(cells?.Changed, defaults.Changed, section, "changed", errors));
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
			FontSize: dto?.FontSize ?? defaults.FontSize,
			Weight: dto?.Weight ?? defaults.Weight,
			Italic: dto?.Italic ?? defaults.Italic);
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
			and not GridOrientationValues.RowsAsSteps
			and not GridOrientationValues.ColumnsAsSteps)
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

	private static double OptionalWeight(
		double? value,
		double fallback,
		string section,
		string key,
		List<IError> errors)
	{
		if (value is null)
		{
			return fallback;
		}

		if (value is < 0 or > 1 || double.IsNaN(value.Value))
		{
			errors.Add(new GridStyleWeightOutOfRangeError(section, key, value.Value));
			return fallback;
		}

		return value.Value;
	}
}
