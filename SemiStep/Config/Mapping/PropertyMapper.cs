using Config.Dto;

using Shared.Entities;

namespace Config.Mapping;

public sealed class PropertyMapper : IEntityMapper<PropertyDto, PropertyDefinition>
{
	public PropertyDefinition Map(PropertyDto dto)
	{
		if (string.IsNullOrWhiteSpace(dto.PropertyTypeId))
		{
			throw new InvalidOperationException("PropertyTypeId is required for mapping");
		}

		if (string.IsNullOrWhiteSpace(dto.SystemType))
		{
			throw new InvalidOperationException($"SystemType is required for property '{dto.PropertyTypeId}'");
		}

		if (string.IsNullOrWhiteSpace(dto.FormatKind))
		{
			throw new InvalidOperationException($"FormatKind is required for property '{dto.PropertyTypeId}'");
		}

		return new PropertyDefinition(
			PropertyTypeId: dto.PropertyTypeId,
			SystemType: dto.SystemType,
			FormatKind: dto.FormatKind,
			Units: dto.Units,
			Min: dto.Min,
			Max: dto.Max,
			MaxLength: dto.MaxLength);
	}

	public IReadOnlyList<PropertyDefinition> MapMany(IEnumerable<PropertyDto> dtos)
	{
		return dtos.Select(Map).ToList();
	}
}
