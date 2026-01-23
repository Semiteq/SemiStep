using Config.Dto;

using Shared.Entities;

namespace Config.Mapping;

public sealed class ColumnMapper : IEntityMapper<ColumnDto, ColumnDefinition>
{
	public ColumnDefinition Map(ColumnDto dto)
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

		return new ColumnDefinition(
			Key: dto.Key,
			ColumnType: dto.ColumnType,
			UiName: dto.Ui.UiName ?? string.Empty,
			Width: dto.Ui.Width,
			PropertyTypeId: dto.BusinessLogic.PropertyTypeId ?? string.Empty,
			PlcDataType: dto.BusinessLogic.PlcDataType ?? string.Empty,
			ReadOnly: dto.BusinessLogic.ReadOnly,
			SaveToCsv: dto.BusinessLogic.SaveToCsv);
	}

	public IReadOnlyList<ColumnDefinition> MapMany(IEnumerable<ColumnDto> dtos)
	{
		return dtos.Select(Map).ToList();
	}
}
