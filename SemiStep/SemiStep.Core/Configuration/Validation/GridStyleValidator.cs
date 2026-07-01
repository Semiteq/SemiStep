using System.Text.RegularExpressions;

using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Validation;

internal static class GridStyleValidator
{
	private static readonly Regex _hexColorRegex = new(
		"^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$",
		RegexOptions.Compiled);

	public static Result Validate(GridStyleOptionsDto? dto)
	{
		if (dto is null)
		{
			return Result.Fail("Grid style configuration is missing (ui/grid_style.yaml).");
		}

		if (dto.Colors is null)
		{
			return Result.Fail("Grid style configuration is missing 'colors' section.");
		}

		var errors = new List<IError>();

		var execution = dto.Colors.Cells?.Execution;
		ValidateSection(
			"colors.cells.execution",
			execution is null
				? null
				: new (string Name, string? Value)[]
				{
					("depth_0", execution.Depth0),
					("depth_1", execution.Depth1),
					("depth_2", execution.Depth2),
					("depth_3", execution.Depth3),
					("depth_0_past", execution.Depth0Past),
					("depth_1_past", execution.Depth1Past),
					("depth_2_past", execution.Depth2Past),
					("depth_3_past", execution.Depth3Past),
					("current_step_marker", execution.CurrentStepMarker)
				},
			errors);

		var readOnlyCells = dto.Colors.Cells?.ReadOnly;
		ValidateSection(
			"colors.cells.readonly",
			readOnlyCells is null
				? null
				: new (string Name, string? Value)[]
				{
					("depth_0", readOnlyCells.Depth0),
					("depth_1", readOnlyCells.Depth1),
					("depth_2", readOnlyCells.Depth2),
					("depth_3", readOnlyCells.Depth3),
					("depth_0_past", readOnlyCells.Depth0Past),
					("depth_1_past", readOnlyCells.Depth1Past),
					("depth_2_past", readOnlyCells.Depth2Past),
					("depth_3_past", readOnlyCells.Depth3Past),
					("selected", readOnlyCells.Selected),
					("foreground", readOnlyCells.Foreground)
				},
			errors);

		var disabled = dto.Colors.Cells?.Disabled;
		ValidateSection(
			"colors.cells.disabled",
			disabled is null
				? null
				: new (string Name, string? Value)[]
				{
					("depth_0", disabled.Depth0),
					("depth_1", disabled.Depth1),
					("depth_2", disabled.Depth2),
					("depth_3", disabled.Depth3),
					("depth_0_past", disabled.Depth0Past),
					("depth_1_past", disabled.Depth1Past),
					("depth_2_past", disabled.Depth2Past),
					("depth_3_past", disabled.Depth3Past),
					("selected", disabled.Selected),
					("foreground", disabled.Foreground)
				},
			errors);

		var chrome = dto.Chrome;
		ValidateOptionalSection(
			"chrome",
			chrome is null
				? null
				: new (string Name, string? Value)[]
				{
					("info", chrome.Info),
					("connected", chrome.Connected),
					("disconnected", chrome.Disconnected),
					("local_mode", chrome.LocalMode),
					("panel_background", chrome.PanelBackground),
					("panel_header_background", chrome.PanelHeaderBackground),
					("subtle_border", chrome.SubtleBorder),
					("separator", chrome.Separator),
					("secondary_foreground", chrome.SecondaryForeground),
					("grid_border", chrome.GridBorder),
					("grid_background", chrome.GridBackground),
					("header_foreground", chrome.HeaderForeground)
				},
			errors);

		var selection = dto.Colors.Selection;
		ValidateOptionalSection(
			"colors.selection",
			selection is null
				? null
				: new (string Name, string? Value)[]
				{
					("background", selection.Background),
					("foreground", selection.Foreground)
				},
			errors);

		var cells = dto.Colors.Cells;
		ValidateOptionalSection(
			"colors.cells",
			cells is null
				? null
				: new (string Name, string? Value)[]
				{
					("changed", cells.Changed),
					("changed_selected", cells.ChangedSelected)
				},
			errors);

		if (dto.Colors.GridLine is not null)
		{
			ValidateKey("colors", "grid_line", dto.Colors.GridLine, errors);
		}

		var statusBar = dto.StatusBar;
		ValidateOptionalSection(
			"status_bar",
			statusBar is null
				? null
				: new (string Name, string? Value)[]
				{
					("background", statusBar.Background),
					("foreground", statusBar.Foreground)
				},
			errors);

		var validationPanel = dto.ValidationPanel;
		ValidateOptionalSection(
			"validation_panel",
			validationPanel is null
				? null
				: new (string Name, string? Value)[]
				{
					("background", validationPanel.Background),
					("foreground", validationPanel.Foreground),
					("error_color", validationPanel.ErrorColor),
					("warning_color", validationPanel.WarningColor)
				},
			errors);

		if (errors.Count > 0)
		{
			return Result.Fail(errors);
		}

		return Result.Ok();
	}

	private static void ValidateSection(
		string sectionPath,
		IReadOnlyList<(string Name, string? Value)>? keys,
		List<IError> errors)
	{
		if (keys is null)
		{
			errors.Add(new Error($"Grid style configuration is missing '{sectionPath}' section."));
			return;
		}

		foreach (var (name, value) in keys)
		{
			ValidateKey(sectionPath, name, value, errors);
		}
	}

	private static void ValidateOptionalSection(
		string sectionPath,
		IReadOnlyList<(string Name, string? Value)>? keys,
		List<IError> errors)
	{
		if (keys is null)
		{
			return;
		}

		foreach (var (name, value) in keys)
		{
			if (value is null)
			{
				continue;
			}

			ValidateKey(sectionPath, name, value, errors);
		}
	}

	private static void ValidateKey(string sectionPath, string keyName, string? value, List<IError> errors)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			errors.Add(new Error($"Grid style '{sectionPath}.{keyName}' is missing or empty."));
			return;
		}

		if (!_hexColorRegex.IsMatch(value))
		{
			errors.Add(new Error(
				$"Grid style '{sectionPath}.{keyName}' has invalid hex color: '{value}'. " +
				"Expected format: '#RRGGBB' or '#AARRGGBB'."));
		}
	}
}
