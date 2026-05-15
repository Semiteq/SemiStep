using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;

using SemiStep.UI.Clipboard;
using SemiStep.UI.Coordinator;
using SemiStep.UI.MainWindow;
using SemiStep.UI.MessageService;
using SemiStep.UI.Plc;
using SemiStep.UI.RecipeFile;
using SemiStep.UI.RecipeGrid;

namespace SemiStep.UI;

public static class UiDi
{
	public static IServiceCollection AddUi(this IServiceCollection services)
	{
		services.AddSingleton(sp => sp.GetRequiredService<AppConfiguration>().GridStyle);
		services.AddSingleton<MessagePanelViewModel>();
		services.AddSingleton<RecipeCoordinator>();
		services.AddSingleton<RecipeGridViewModel>();
		services.AddSingleton<RecipeCommandsViewModel>();
		services.AddSingleton<ClipboardViewModel>();
		services.AddSingleton<RecipeFileViewModel>();
		services.AddSingleton<ColumnBuilder>();
		services.AddSingleton<PlcMonitorViewModel>();
		services.AddSingleton<MainWindowViewModel>();

		return services;
	}
}
