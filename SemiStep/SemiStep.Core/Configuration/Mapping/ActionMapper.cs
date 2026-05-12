using SemiStep.Core.Configuration.Dto;

using SemiStep.Core.Recipes;

namespace SemiStep.Core.Configuration.Mapping;

internal static class ActionMapper
{
	public static ActionDefinition Map(ActionDto dto)
	{
		if (dto.Id <= 0)
		{
			throw new InvalidOperationException("Action Id must be positive for mapping");
		}

		if (string.IsNullOrWhiteSpace(dto.UiName))
		{
			throw new InvalidOperationException($"UiName is required for action Id={dto.Id}");
		}

		if (string.IsNullOrWhiteSpace(dto.DeployDuration))
		{
			throw new InvalidOperationException($"DeployDuration is required for action Id={dto.Id}");
		}

		var columns = dto.Columns?
			.Select(MapColumn)
			.ToList() ?? [];

		var deployDuration = MapDeployDuration(dto.DeployDuration, dto.Id);

		return new ActionDefinition(
			Id: dto.Id,
			UiName: dto.UiName,
			DeployDuration: deployDuration,
			Properties: columns);
	}

	private static DeployDuration MapDeployDuration(string? value, int actionId)
	{
		return value switch
		{
			"immediate" => DeployDuration.Immediate,
			"longlasting" => DeployDuration.LongLasting,
			_ => throw new InvalidOperationException(
				$"Unsupported DeployDuration '{value}' for action Id={actionId}")
		};
	}

	public static IReadOnlyList<ActionDefinition> MapMany(IEnumerable<ActionDto> dtos)
	{
		return dtos.Select(Map).ToList();
	}

	private static ActionPropertyDefinition MapColumn(ActionColumnDto dto)
	{
		if (string.IsNullOrWhiteSpace(dto.Key))
		{
			throw new InvalidOperationException("Action column Key is required for mapping");
		}

		if (string.IsNullOrWhiteSpace(dto.PropertyTypeId))
		{
			throw new InvalidOperationException($"PropertyTypeId is required for action column '{dto.Key}'");
		}

		return new ActionPropertyDefinition(
			Key: dto.Key,
			GroupName: dto.GroupName,
			PropertyTypeId: dto.PropertyTypeId,
			DefaultValue: dto.DefaultValue);
	}
}
