using Config.Dto;

using TypesShared.Core;

namespace Config.Mapping;

internal static class PropertyMapper
{
	public static PropertyTypeDefinition Map(PropertyDto dto)
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

		return new PropertyTypeDefinition(
			Id: dto.PropertyTypeId,
			SystemType: dto.SystemType,
			FormatKind: dto.FormatKind,
			Units: dto.Units,
			Min: dto.Min,
			Max: dto.Max,
			MaxLength: dto.MaxLength);
	}

	public static IReadOnlyList<PropertyTypeDefinition> MapMany(IEnumerable<PropertyDto> dtos)
	{
		return dtos.Select(Map).ToList();
	}
}
