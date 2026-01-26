using Microsoft.Extensions.DependencyInjection;

using UI.ViewModels;

namespace UI;

public static class UiDi
{
	public static IServiceCollection AddUi(this IServiceCollection services)
	{
		services.AddSingleton<MainWindowViewModel>();

		return services;
	}
}
