using Config.Dto;

using Shared.Entities;

namespace Config.Mapping;

public sealed class ActionMapper : IEntityMapper<ActionDto, ActionDefinition>
{
	public ActionDefinition Map(ActionDto dto)
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

		return new ActionDefinition(
			Id: dto.Id,
			UiName: dto.UiName,
			DeployDuration: dto.DeployDuration,
			Columns: columns);
	}

	public IReadOnlyList<ActionDefinition> MapMany(IEnumerable<ActionDto> dtos)
	{
		return dtos.Select(Map).ToList();
	}

	private static ActionColumnDefinition MapColumn(ActionColumnDto dto)
	{
		if (string.IsNullOrWhiteSpace(dto.Key))
		{
			throw new InvalidOperationException("Action column Key is required for mapping");
		}

		if (string.IsNullOrWhiteSpace(dto.PropertyTypeId))
		{
			throw new InvalidOperationException($"PropertyTypeId is required for action column '{dto.Key}'");
		}

		return new ActionColumnDefinition(
			Key: dto.Key,
			GroupName: dto.GroupName,
			PropertyTypeId: dto.PropertyTypeId,
			DefaultValue: dto.DefaultValue);
	}
}
