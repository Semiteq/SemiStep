using System.Collections.Immutable;
using System.Globalization;

using Csv.FsService;

using FluentResults;

using Shared.Config;
using Shared.Config.Contracts;
using Shared.Core;

namespace Csv.Helpers;

internal static class CsvStepReader
{
	internal static Result<int> TryParseActionValue(string? rawAction, int rowNumber, IActionRegistry actionRegistry)
	{
		if (string.IsNullOrWhiteSpace(rawAction))
		{
			return Result.Fail($"Row {rowNumber}: action column is empty");
		}

		if (!int.TryParse(rawAction, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actionKey))
		{
			return Result.Fail($"Row {rowNumber}: cannot parse action value '{rawAction}' as integer");
		}

		if (!actionRegistry.ActionExists(actionKey))
		{
			return Result.Fail($"Row {rowNumber}: unknown action ID '{actionKey}'");
		}

		return actionKey;
	}

	internal static HashSet<string> GetActionColumnKeys(int actionKey, IActionRegistry actionRegistry)
	{
		var action = actionRegistry.GetAction(actionKey);

		return action.Columns
			.Select(c => c.Key)
			.ToHashSet();
	}

	internal static Result<ImmutableDictionary<ColumnId, PropertyValue>> ParseProperties(
		IReadOnlyList<GridColumnDefinition> columns,
		HashSet<string> actionColumnKeys,
		int rowNumber,
		IPropertyRegistry propertyRegistry,
		Func<GridColumnDefinition, string?> getField)
	{
		var errors = new List<IError>();
		var properties = ImmutableDictionary.CreateBuilder<ColumnId, PropertyValue>();

		foreach (var column in columns)
		{
			if (column.Key == CsvStepWriter.ActionColumnKey)
			{
				continue;
			}

			var rawValue = getField(column);
			if (string.IsNullOrWhiteSpace(rawValue))
			{
				continue;
			}

			if (!actionColumnKeys.Contains(column.Key))
			{
				continue;
			}

			var propertyDef = propertyRegistry.GetProperty(column.PropertyTypeId);
			var propertyResult = StepValueParser.TryParsePropertyValue(rawValue, propertyDef, column.Key, rowNumber);
			if (propertyResult.IsFailed)
			{
				errors.AddRange(propertyResult.Errors);
			}
			else
			{
				properties.Add(new ColumnId(column.Key), propertyResult.Value);
			}
		}

		if (errors.Count > 0)
		{
			return Result.Fail(errors);
		}

		return properties.ToImmutable();
	}
}
