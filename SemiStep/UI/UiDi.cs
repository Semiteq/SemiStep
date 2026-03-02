using Domain.Facade;

using Microsoft.Extensions.DependencyInjection;

using Shared;
using Shared.Registries;

using UI.Services;
using UI.ViewModels;

namespace UI;

public static class UiDi
{
	public static IServiceCollection AddUi(this IServiceCollection services)
	{
		services.AddSingleton<INotificationService, NotificationService>();
		services.AddSingleton<MainWindowViewModel>(sp =>
		{
			Action shutdownApplication = () =>
			{
				if (Avalonia.Application.Current?.ApplicationLifetime
					is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
				{
					lifetime.Shutdown();
				}
			};

			return new MainWindowViewModel(
				sp.GetRequiredService<AppConfiguration>(),
				sp.GetRequiredService<DomainFacade>(),
				sp.GetRequiredService<IActionRegistry>(),
				sp.GetRequiredService<IGroupRegistry>(),
				sp.GetRequiredService<IColumnRegistry>(),
				sp.GetRequiredService<INotificationService>(),
				shutdownApplication);
		});

		return services;
	}
}
