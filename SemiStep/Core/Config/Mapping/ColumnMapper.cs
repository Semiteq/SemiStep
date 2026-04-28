using Config.Dto;

using TypesShared.Config;

namespace Config.Mapping;

internal static class ColumnMapper
{
	public static GridColumnDefinition Map(ColumnDto dto)
	{
		if (string.IsNullOrWhiteSpace(dto.Key))
		{
			throw new InvalidOperationException("Column Key is required for mapping");
		}

		if (string.IsNullOrWhiteSpace(dto.ColumnType))
		{
			throw new InvalidOperationException($"ColumnType is required for column '{dto.Key}'");
		}

		if (dto.Ui == null)
		{
			throw new InvalidOperationException($"Ui section is required for column '{dto.Key}'");
		}

		if (dto.BusinessLogic == null)
		{
			throw new InvalidOperationException($"BusinessLogic section is required for column '{dto.Key}'");
		}

		return new GridColumnDefinition(
			Key: dto.Key,
			ColumnType: dto.ColumnType,
			UiName: dto.Ui.UiName ?? string.Empty,
			PropertyTypeId: dto.BusinessLogic.PropertyTypeId ?? string.Empty,
			PlcDataType: dto.BusinessLogic.PlcDataType ?? string.Empty,
			ReadOnly: dto.BusinessLogic.ReadOnly,
			SaveToCsv: dto.BusinessLogic.SaveToCsv);
	}

	public static IReadOnlyList<GridColumnDefinition> MapMany(IEnumerable<ColumnDto> dtos)
	{
		return dtos.Select(Map).ToList();
	}
}
