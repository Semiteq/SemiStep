using FluentResults;

using NCalc.Domain;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Formulas;

namespace SemiStep.Core.Configuration.Mapping;

internal static class ActionMapper
{
	public static Result<ActionDefinition> TryMap(
		ActionDto dto,
		IReadOnlyDictionary<string, PropertyTypeDefinition> properties)
	{
		if (dto.Id <= 0)
		{
			return Result.Fail($"Action Id must be positive for mapping (got {dto.Id})");
		}

		if (string.IsNullOrWhiteSpace(dto.UiName))
		{
			return Result.Fail($"UiName is required for action Id={dto.Id}");
		}

		if (string.IsNullOrWhiteSpace(dto.DeployDuration))
		{
			return Result.Fail($"DeployDuration is required for action Id={dto.Id}");
		}

		var roleResult = MapRole(dto.Role, dto.Id);
		if (roleResult.IsFailed)
		{
			return roleResult.ToResult<ActionDefinition>();
		}

		var columns = new List<ActionPropertyDefinition>();
		if (dto.Columns is not null)
		{
			foreach (var columnDto in dto.Columns)
			{
				var columnResult = TryMapColumn(columnDto);
				if (columnResult.IsFailed)
				{
					return columnResult.ToResult<ActionDefinition>();
				}

				columns.Add(columnResult.Value);
			}
		}

		var deployDurationResult = MapDeployDuration(dto.DeployDuration, dto.Id);
		if (deployDurationResult.IsFailed)
		{
			return deployDurationResult.ToResult<ActionDefinition>();
		}

		var formulaResult = MapFormula(dto.Id, dto.UiName, dto.Formula, columns, properties);
		if (formulaResult.IsFailed)
		{
			return formulaResult.ToResult<ActionDefinition>();
		}

		return Result.Ok(new ActionDefinition(
			id: dto.Id,
			uiName: dto.UiName,
			deployDuration: deployDurationResult.Value,
			properties: columns,
			formula: formulaResult.Value,
			role: roleResult.Value));
	}

	public static Result<IReadOnlyList<ActionDefinition>> TryMapMany(
		IEnumerable<ActionDto> dtos,
		IReadOnlyDictionary<string, PropertyTypeDefinition> properties)
	{
		var results = new List<ActionDefinition>();
		var failures = new List<Result>();

		foreach (var dto in dtos)
		{
			var mapped = TryMap(dto, properties);
			if (mapped.IsFailed)
			{
				failures.Add(mapped.ToResult());
			}
			else
			{
				results.Add(mapped.Value);
			}
		}

		if (failures.Count > 0)
		{
			return Result.Merge(failures.ToArray()).ToResult<IReadOnlyList<ActionDefinition>>();
		}

		return Result.Ok<IReadOnlyList<ActionDefinition>>(results);
	}

	private static Result<ActionRole> MapRole(string? value, int actionId)
	{
		return value switch
		{
			null => Result.Ok(ActionRole.Action),
			"action" => Result.Ok(ActionRole.Action),
			"subaction" => Result.Ok(ActionRole.Subaction),
			_ => Result.Fail<ActionRole>(
				$"Unsupported role '{value}' for action Id={actionId} (expected 'action' or 'subaction')")
		};
	}

	private static Result<DeployDuration> MapDeployDuration(string? value, int actionId)
	{
		return value switch
		{
			"immediate" => Result.Ok(DeployDuration.Immediate),
			"longlasting" => Result.Ok(DeployDuration.LongLasting),
			_ => Result.Fail<DeployDuration>(
				$"Unsupported DeployDuration '{value}' for action Id={actionId}")
		};
	}

	private static Result<ActionPropertyDefinition> TryMapColumn(ActionColumnDto dto)
	{
		if (string.IsNullOrWhiteSpace(dto.Key))
		{
			return Result.Fail<ActionPropertyDefinition>("Action column Key is required for mapping");
		}

		if (string.IsNullOrWhiteSpace(dto.PropertyTypeId))
		{
			return Result.Fail<ActionPropertyDefinition>(
				$"PropertyTypeId is required for action column '{dto.Key}'");
		}

		return Result.Ok(new ActionPropertyDefinition(
			Key: dto.Key,
			GroupName: dto.GroupName,
			PropertyTypeId: dto.PropertyTypeId,
			DefaultValue: dto.DefaultValue,
			Targets: dto.Targets));
	}

	private static Result<FormulaDefinition?> MapFormula(
		int actionId,
		string actionName,
		FormulaDto? formulaDto,
		IReadOnlyList<ActionPropertyDefinition> columns,
		IReadOnlyDictionary<string, PropertyTypeDefinition> properties)
	{
		if (formulaDto is null)
		{
			return Result.Ok<FormulaDefinition?>(null);
		}

		var section = $"actions, Id={actionId}, UiName='{actionName}'";

		var rawRecalcOrder = formulaDto.RecalcOrder ?? new List<string>();
		var rawExpressions = formulaDto.Expressions ?? new Dictionary<string, string>();

		var failures = new List<Result>();

		// Normalize expression dictionary to a case-insensitive lookup once; reject case-only duplicates.
		var expressionsByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (key, value) in rawExpressions)
		{
			if (expressionsByKey.ContainsKey(key))
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.expressions contains duplicate entry for '{key}' (case-insensitive)"));
				continue;
			}

			expressionsByKey[key] = value;
		}

		if (rawRecalcOrder.Count < 2)
		{
			failures.Add(Result.Fail(
				$"[{section}] formula.recalc_order must contain at least two entries (got {rawRecalcOrder.Count})"));
		}

		var distinctRecalcOrder = new HashSet<string>(rawRecalcOrder, StringComparer.OrdinalIgnoreCase);
		if (distinctRecalcOrder.Count != rawRecalcOrder.Count)
		{
			failures.Add(Result.Fail(
				$"[{section}] formula.recalc_order contains duplicate entries"));
		}

		// Map case-insensitively to action column key (canonical casing).
		var columnByKey = new Dictionary<string, ActionPropertyDefinition>(StringComparer.OrdinalIgnoreCase);
		foreach (var column in columns)
		{
			columnByKey[column.Key] = column;
		}

		// Normalize recalc_order entries to canonical column casing.
		var canonicalRecalcOrder = new List<string>(rawRecalcOrder.Count);
		foreach (var variable in rawRecalcOrder)
		{
			if (columnByKey.TryGetValue(variable, out var column))
			{
				canonicalRecalcOrder.Add(column.Key);
			}
			else
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.recalc_order entry '{variable}' is not a column of this action"));
			}
		}

		// Validate system_type is numeric (int/float).
		foreach (var variable in canonicalRecalcOrder)
		{
			if (!columnByKey.TryGetValue(variable, out var column))
			{
				continue;
			}

			if (!properties.TryGetValue(column.PropertyTypeId, out var propertyDef))
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.recalc_order entry '{variable}' references unknown property type '{column.PropertyTypeId}'"));
				continue;
			}

			if (!SystemTypes.Comparer.Equals(propertyDef.SystemType, SystemTypes.Int)
				&& !SystemTypes.Comparer.Equals(propertyDef.SystemType, SystemTypes.Float))
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.recalc_order entry '{variable}' has non-numeric system_type '{propertyDef.SystemType}'"));
			}
		}

		foreach (var variable in canonicalRecalcOrder)
		{
			if (!expressionsByKey.ContainsKey(variable))
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.expressions is missing entry for recalc_order variable '{variable}'"));
			}
		}

		foreach (var expressionKey in expressionsByKey.Keys)
		{
			if (!distinctRecalcOrder.Contains(expressionKey))
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.expressions contains entry '{expressionKey}' that is not in recalc_order"));
			}
		}

		var compiled = new Dictionary<string, LogicalExpression>(StringComparer.Ordinal);

		foreach (var variable in canonicalRecalcOrder)
		{
			if (!expressionsByKey.TryGetValue(variable, out var expressionSource))
			{
				continue;
			}

			var parseResult = FormulaIdentifierExtractor.Parse(expressionSource);
			if (parseResult.IsFailed)
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.expressions['{variable}'] failed to parse expression '{expressionSource}': "
					+ string.Join("; ", parseResult.Errors.Select(e => e.Message))));
				continue;
			}

			var (logicalExpression, identifiers) = parseResult.Value;

			foreach (var identifier in identifiers)
			{
				if (!distinctRecalcOrder.Contains(identifier)
					|| !columnByKey.TryGetValue(identifier, out var column))
				{
					failures.Add(Result.Fail(
						$"[{section}] formula.expressions['{variable}'] references variable '{identifier}' "
						+ $"that is not declared in recalc_order"));
					continue;
				}

				if (!string.Equals(identifier, column.Key, StringComparison.Ordinal))
				{
					failures.Add(Result.Fail(
						$"[{section}] formula.expressions['{variable}'] references variable '{identifier}' "
						+ $"with casing that does not match recalc_order entry '{column.Key}'"));
				}
			}

			compiled[variable] = logicalExpression;
		}

		if (failures.Count > 0)
		{
			return Result.Merge(failures.ToArray()).ToResult<FormulaDefinition?>();
		}

		var definition = new FormulaDefinition(
			recalcOrder: canonicalRecalcOrder,
			compiledExpressions: compiled);

		return Result.Ok<FormulaDefinition?>(definition);
	}
}
