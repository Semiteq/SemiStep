using Microsoft.Extensions.DependencyInjection;

using SemiStep.Application.Services;
using SemiStep.Application.State;

namespace SemiStep.Application;

public static class ApplicationServiceCollectionExtensions
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddSingleton<RecipeStateManager>();
		services.AddSingleton<RecipeApplicationService>();

		return services;
	}
}
