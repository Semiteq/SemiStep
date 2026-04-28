using System.Globalization;

using Config.Dto;

using FluentResults;

using TypesShared.Results;

namespace Config.Validation;

internal static class DefaultValueValidator
{
	public static Result Validate(
		List<PropertyDto> properties,
		List<ColumnDto> columns,
		List<ActionDto> actions)
	{
		var propertyByTypeId = BuildPropertyLookup(properties);
		var columnByKey = BuildColumnLookup(columns);
		var validationResults = new List<Result>();

		foreach (var action in actions)
		{
			if (action.Columns is null)
			{
				continue;
			}

			foreach (var actionColumn in action.Columns)
			{
				ValidateActionColumn(action, actionColumn, propertyByTypeId, columnByKey, validationResults);
			}
		}

		if (validationResults.Count == 0)
		{
			return Result.Ok();
		}

		return Result.Merge(validationResults.ToArray());
	}

	private static void ValidateActionColumn(
		ActionDto action,
		ActionColumnDto actionColumn,
		Dictionary<string, PropertyDto> propertyByTypeId,
		Dictionary<string, ColumnDto> columnByKey,
		List<Result> validationResults)
	{
		if (string.IsNullOrEmpty(actionColumn.DefaultValue))
		{
			return;
		}

		var location = $"actions, Id={action.Id}, column '{actionColumn.Key}'";

		ValidateReadOnlyConflict(actionColumn, columnByKey, location, validationResults);
		ValidateValueAgainstProperty(actionColumn, propertyByTypeId, location, validationResults);
	}

	private static void ValidateReadOnlyConflict(
		ActionColumnDto actionColumn,
		Dictionary<string, ColumnDto> columnByKey,
		string location,
		List<Result> validationResults)
	{
		if (string.IsNullOrEmpty(actionColumn.Key))
		{
			return;
		}

		if (!columnByKey.TryGetValue(actionColumn.Key, out var columnDto))
		{
			return;
		}

		if (columnDto.BusinessLogic?.ReadOnly == true)
		{
			validationResults.Add(Result.Ok().WithWarning(
				$"[{location}] Column '{actionColumn.Key}' is read_only but has a default_value defined; the default value will have no effect"));
		}
	}

	private static void ValidateValueAgainstProperty(
		ActionColumnDto actionColumn,
		Dictionary<string, PropertyDto> propertyByTypeId,
		string location,
		List<Result> validationResults)
	{
		if (string.IsNullOrEmpty(actionColumn.PropertyTypeId))
		{
			return;
		}

		if (!propertyByTypeId.TryGetValue(actionColumn.PropertyTypeId, out var propertyDto))
		{
			return;
		}

		var systemType = propertyDto.SystemType?.ToLowerInvariant();
		var defaultValue = actionColumn.DefaultValue!;

		switch (systemType)
		{
			case "string":
				ValidateStringDefault(defaultValue, propertyDto, location, validationResults);
				break;
			case "int":
				ValidateIntDefault(defaultValue, propertyDto, location, validationResults);
				break;
			case "float":
				ValidateFloatDefault(defaultValue, propertyDto, location, validationResults);
				break;
		}
	}

	private static void ValidateStringDefault(
		string defaultValue,
		PropertyDto propertyDto,
		string location,
		List<Result> validationResults)
	{
		if (propertyDto.MaxLength.HasValue && defaultValue.Length > propertyDto.MaxLength.Value)
		{
			validationResults.Add(Result.Fail(
				$"[{location}] Default value exceeds max_length ({propertyDto.MaxLength.Value}): " +
				$"value has {defaultValue.Length} characters"));
		}
	}

	private static void ValidateIntDefault(
		string defaultValue,
		PropertyDto propertyDto,
		string location,
		List<Result> validationResults)
	{
		if (!int.TryParse(defaultValue, CultureInfo.InvariantCulture, out var parsed))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Cannot parse default value '{defaultValue}' as int"));
			return;
		}

		ValidateNumericRange(parsed, propertyDto, location, validationResults);
	}

	private static void ValidateFloatDefault(
		string defaultValue,
		PropertyDto propertyDto,
		string location,
		List<Result> validationResults)
	{
		if (!float.TryParse(defaultValue, CultureInfo.InvariantCulture, out var parsed))
		{
			validationResults.Add(
				Result.Fail($"[{location}] Cannot parse default value '{defaultValue}' as float"));
			return;
		}

		ValidateNumericRange(parsed, propertyDto, location, validationResults);
	}

	private static void ValidateNumericRange(
		double value,
		PropertyDto propertyDto,
		string location,
		List<Result> validationResults)
	{
		if (propertyDto.Min.HasValue && value < propertyDto.Min.Value)
		{
			validationResults.Add(Result.Fail(string.Format(
				CultureInfo.InvariantCulture,
				"[{0}] Default value {1} is out of range: minimum is {2}",
				location, value, propertyDto.Min.Value)));
		}

		if (propertyDto.Max.HasValue && value > propertyDto.Max.Value)
		{
			validationResults.Add(Result.Fail(string.Format(
				CultureInfo.InvariantCulture,
				"[{0}] Default value {1} is out of range: maximum is {2}",
				location, value, propertyDto.Max.Value)));
		}
	}

	private static Dictionary<string, PropertyDto> BuildPropertyLookup(List<PropertyDto> properties)
	{
		var lookup = new Dictionary<string, PropertyDto>(StringComparer.OrdinalIgnoreCase);

		foreach (var property in properties)
		{
			if (!string.IsNullOrEmpty(property.PropertyTypeId))
			{
				lookup.TryAdd(property.PropertyTypeId, property);
			}
		}

		return lookup;
	}

	private static Dictionary<string, ColumnDto> BuildColumnLookup(List<ColumnDto> columns)
	{
		var lookup = new Dictionary<string, ColumnDto>(StringComparer.OrdinalIgnoreCase);

		foreach (var column in columns)
		{
			if (!string.IsNullOrEmpty(column.Key))
			{
				lookup.TryAdd(column.Key, column);
			}
		}

		return lookup;
	}
}
