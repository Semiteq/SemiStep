using Microsoft.Extensions.DependencyInjection;

using Serilog;

using UI.ViewModels;

namespace UI;

public static class UiDi
{
	public static IServiceCollection AddUi(this IServiceCollection services, ILogger? logger = null)
	{
		if (logger is not null)
		{
			services.AddSingleton(logger);
		}

		services.AddSingleton<MainWindowViewModel>();

		return services;
	}
}
