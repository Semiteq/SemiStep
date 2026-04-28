using System.Collections.Immutable;
using System.Globalization;

using FluentResults;

using TypesShared.Config;
using TypesShared.Core;

namespace Csv;

public sealed class CsvRowConverter(AppConfiguration config, IPropertyParser propertyParser)
{
	private const char Separator = ';';
	private const string ActionColumnKey = StepValueParser.ActionColumnKey;

	private readonly List<string> _columnOrder = config.Columns.Values
		.Where(c => c.SaveToCsv)
		.Select(c => c.Key)
		.ToList();

	public IReadOnlyList<string> ColumnOrder => _columnOrder;

	public Result<Step> Convert(string csvLine)
	{
		var rawFields = csvLine.Split(Separator);

		var actionIndex = _columnOrder.IndexOf(ActionColumnKey);
		if (actionIndex < 0 || actionIndex >= rawFields.Length)
		{
			return Result.Fail("Action column not found");
		}

		var rawAction = rawFields[actionIndex].Trim();
		if (string.IsNullOrWhiteSpace(rawAction))
		{
			return Result.Fail("Action column is empty");
		}

		if (!int.TryParse(rawAction, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actionKey))
		{
			return Result.Fail($"Cannot parse action value '{rawAction}' as integer");
		}

		if (!config.Actions.TryGetValue(actionKey, out var actionDef))
		{
			return Result.Fail($"Unknown action ID '{actionKey}'");
		}

		var actionPropertyKeys = actionDef.Properties
			.Select(p => p.Key)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var errors = new List<IError>();
		var properties = ImmutableDictionary.CreateBuilder<PropertyId, PropertyValue>();

		for (var i = 0; i < _columnOrder.Count && i < rawFields.Length; i++)
		{
			var columnKey = _columnOrder[i];
			if (columnKey == ActionColumnKey)
			{
				continue;
			}

			var rawValue = rawFields[i].Trim();
			if (string.IsNullOrWhiteSpace(rawValue))
			{
				continue;
			}

			if (!actionPropertyKeys.Contains(columnKey))
			{
				continue;
			}

			if (!config.Columns.TryGetValue(columnKey, out var columnDef))
			{
				continue;
			}

			if (!config.Properties.TryGetValue(columnDef.PropertyTypeId, out var propertyTypeDef))
			{
				continue;
			}

			var parseResult = propertyParser.Parse(rawValue, propertyTypeDef);
			if (parseResult.IsFailed)
			{
				foreach (var e in parseResult.Errors)
				{
					errors.Add(new Error($"Column '{columnKey}': {e.Message}"));
				}
			}
			else
			{
				properties.Add(new PropertyId(columnKey), parseResult.Value);
			}
		}

		if (errors.Count > 0)
		{
			return Result.Fail(errors);
		}

		return Result.Ok(new Step(actionKey, properties.ToImmutable()));
	}
}
