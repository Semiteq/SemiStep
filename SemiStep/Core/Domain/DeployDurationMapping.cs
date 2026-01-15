using Core.Entities;

namespace Core.Domain;

internal static class DeployDurationMapping
{
	internal static DeployDuration ToEntity(this DeployDurationDto dto) =>
		dto switch
		{
			DeployDurationDto.Immediate => DeployDuration.Immediate,
			DeployDurationDto.LongLasting => DeployDuration.LongLasting,
			_ => DeployDuration.Immediate
		};
}
