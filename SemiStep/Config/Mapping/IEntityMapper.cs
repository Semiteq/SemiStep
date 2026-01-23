namespace Config.Mapping;

public interface IEntityMapper<in TDto, out TDomain>
	where TDto : class
	where TDomain : class
{
	TDomain Map(TDto dto);

	IReadOnlyList<TDomain> MapMany(IEnumerable<TDto> dtos);
}
