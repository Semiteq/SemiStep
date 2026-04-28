using Microsoft.Extensions.DependencyInjection;

using SemiStep.Core.Configuration;

using UI.Clipboard;
using UI.Coordinator;
using UI.MainWindow;
using UI.MessageService;
using UI.Plc;
using UI.RecipeFile;
using UI.RecipeGrid;

namespace UI;

public static class UiDi
{
	public static IServiceCollection AddUi(this IServiceCollection services)
	{
		services.AddSingleton(sp => sp.GetRequiredService<AppConfiguration>().GridStyle);
		services.AddSingleton<MessagePanelViewModel>();
		services.AddSingleton<RecipeQueryService>();
		services.AddSingleton<RecipeMutationCoordinator>();
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
