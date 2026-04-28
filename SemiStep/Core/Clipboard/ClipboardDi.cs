using Microsoft.Extensions.DependencyInjection;

namespace ClipBoard;

public static class ClipboardDi
{
	public static IServiceCollection AddClipboard(this IServiceCollection services)
	{
		services.AddSingleton<ClipboardSerializer>();
		services.AddSingleton(sp => new ClipboardService(sp.GetRequiredService<ClipboardSerializer>()));

		return services;
	}
}
