using Core.Metadata;

using Microsoft.Extensions.DependencyInjection;

namespace SemiStep.Config;

public static class ConfigServiceCollectionExtensions
{
	public static IServiceCollection AddConfig(this IServiceCollection services, Action<MetadataProvider> configure)
	{
		var provider = new MetadataProvider();
		configure(provider);
		services.AddSingleton<IMetadataProvider>(provider);

		return services;
	}
}
