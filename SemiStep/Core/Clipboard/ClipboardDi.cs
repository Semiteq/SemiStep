using Microsoft.Extensions.DependencyInjection;

using TypesShared.Domain;

namespace ClipBoard;

public static class ClipboardDi
{
	public static IServiceCollection AddClipboard(this IServiceCollection services)
	{
		services.AddSingleton<ClipboardSerializer>();
		services.AddSingleton<IClipboardService, ClipboardService>();

		return services;
	}
}
