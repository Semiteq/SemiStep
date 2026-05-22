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

		if (dto.Colors.Execution is null)
		{
			return Result.Fail("Grid style configuration is missing 'colors.execution' section.");
		}

		var errors = new List<IError>();
		var execution = dto.Colors.Execution;

		var keys = new (string Name, string? Value)[]
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
		};

		foreach (var (name, value) in keys)
		{
			ValidateKey(name, value, errors);
		}

		if (errors.Count > 0)
		{
			return Result.Fail(errors);
		}

		return Result.Ok();
	}

	private static void ValidateKey(string keyName, string? value, List<IError> errors)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			errors.Add(new Error($"Grid style 'colors.execution.{keyName}' is missing or empty."));
			return;
		}

		if (!_hexColorRegex.IsMatch(value))
		{
			errors.Add(new Error(
				$"Grid style 'colors.execution.{keyName}' has invalid hex color: '{value}'. " +
				"Expected format: '#RRGGBB' or '#AARRGGBB'."));
		}
	}
}
