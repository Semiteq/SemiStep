using FluentResults;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Formulas;

namespace SemiStep.Core.Configuration.Mapping;

internal static class ActionMapper
{
	public static ActionDefinition Map(ActionDto dto)
	{
		var result = TryMap(dto);
		if (result.IsFailed)
		{
			throw new InvalidOperationException(
				string.Join("; ", result.Errors.Select(e => e.Message)));
		}

		return result.Value;
	}

	public static Result<ActionDefinition> TryMap(ActionDto dto)
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

		var formulaResult = MapFormula(dto.Id, dto.UiName, dto.Formula, columns);
		if (formulaResult.IsFailed)
		{
			return formulaResult.ToResult<ActionDefinition>();
		}

		return Result.Ok(new ActionDefinition(
			Id: dto.Id,
			UiName: dto.UiName,
			DeployDuration: deployDurationResult.Value,
			Properties: columns,
			Formula: formulaResult.Value));
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

	public static IReadOnlyList<ActionDefinition> MapMany(IEnumerable<ActionDto> dtos)
	{
		return dtos.Select(Map).ToList();
	}

	public static Result<IReadOnlyList<ActionDefinition>> TryMapMany(IEnumerable<ActionDto> dtos)
	{
		var results = new List<ActionDefinition>();
		var failures = new List<Result>();

		foreach (var dto in dtos)
		{
			var mapped = TryMap(dto);
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
			DefaultValue: dto.DefaultValue));
	}

	private static Result<FormulaDefinition?> MapFormula(
		int actionId,
		string actionName,
		FormulaDto? formulaDto,
		IReadOnlyList<ActionPropertyDefinition> columns)
	{
		if (formulaDto is null)
		{
			return Result.Ok<FormulaDefinition?>(null);
		}

		var section = $"actions, Id={actionId}, UiName='{actionName}'";

		var recalcOrder = formulaDto.RecalcOrder ?? new List<string>();
		var expressions = formulaDto.Expressions ?? new Dictionary<string, string>();

		var failures = new List<Result>();

		if (recalcOrder.Count < 2)
		{
			failures.Add(Result.Fail(
				$"[{section}] formula.recalc_order must contain at least two entries (got {recalcOrder.Count})"));
		}

		var distinctRecalcOrder = new HashSet<string>(recalcOrder, StringComparer.OrdinalIgnoreCase);
		if (distinctRecalcOrder.Count != recalcOrder.Count)
		{
			failures.Add(Result.Fail(
				$"[{section}] formula.recalc_order contains duplicate entries"));
		}

		var columnKeys = new HashSet<string>(
			columns.Select(c => c.Key),
			StringComparer.OrdinalIgnoreCase);

		foreach (var variable in recalcOrder)
		{
			if (!columnKeys.Contains(variable))
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.recalc_order entry '{variable}' is not a column of this action"));
			}
		}

		foreach (var variable in distinctRecalcOrder)
		{
			if (!expressions.Keys.Contains(variable, StringComparer.OrdinalIgnoreCase))
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.expressions is missing entry for recalc_order variable '{variable}'"));
			}
		}

		foreach (var expressionKey in expressions.Keys)
		{
			if (!distinctRecalcOrder.Contains(expressionKey))
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.expressions contains entry '{expressionKey}' that is not in recalc_order"));
			}
		}

		if (failures.Count > 0)
		{
			return Result.Merge(failures.ToArray()).ToResult<FormulaDefinition?>();
		}

		var compiled = new Dictionary<string, NCalc.Domain.LogicalExpression>(
			StringComparer.OrdinalIgnoreCase);
		var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (var variable in recalcOrder)
		{
			var expressionSource = expressions.First(kv =>
				string.Equals(kv.Key, variable, StringComparison.OrdinalIgnoreCase)).Value;

			var parseResult = FormulaIdentifierExtractor.ParseAndCompile(expressionSource);
			if (parseResult.IsFailed)
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.expressions['{variable}'] failed to parse expression '{expressionSource}': "
					+ string.Join("; ", parseResult.Errors.Select(e => e.Message))));
				continue;
			}

			var identifierResult = FormulaIdentifierExtractor.Extract(expressionSource);
			if (identifierResult.IsFailed)
			{
				failures.Add(Result.Fail(
					$"[{section}] formula.expressions['{variable}'] failed identifier extraction: "
					+ string.Join("; ", identifierResult.Errors.Select(e => e.Message))));
				continue;
			}

			foreach (var identifier in identifierResult.Value)
			{
				if (!distinctRecalcOrder.Contains(identifier))
				{
					failures.Add(Result.Fail(
						$"[{section}] formula.expressions['{variable}'] references variable '{identifier}' "
						+ $"that is not declared in recalc_order"));
				}
			}

			compiled[variable] = parseResult.Value;
			sources[variable] = expressionSource;
		}

		if (failures.Count > 0)
		{
			return Result.Merge(failures.ToArray()).ToResult<FormulaDefinition?>();
		}

		var definition = new FormulaDefinition(
			recalcOrder: recalcOrder.ToList(),
			expressionSources: sources,
			compiledExpressions: compiled);

		return Result.Ok<FormulaDefinition?>(definition);
	}
}
