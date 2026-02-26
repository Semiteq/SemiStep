using Microsoft.Extensions.DependencyInjection;

using Serilog;

using UI.Services;
using UI.ViewModels;

namespace UI;

public static class UiDi
{
	public static IServiceCollection AddUi(this IServiceCollection services)
	{
		services.AddSingleton<INotificationService, NotificationService>();
		services.AddSingleton<MainWindowViewModel>();

		return services;
	}
}
