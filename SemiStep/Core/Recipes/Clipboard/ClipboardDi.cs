using Microsoft.Extensions.DependencyInjection;

namespace SemiStep.Core.Recipes.Clipboard;

public static class ClipboardDi
{
	public static IServiceCollection AddClipboard(this IServiceCollection services)
	{
		services.AddSingleton<ClipboardSerializer>();

		return services;
	}
}
