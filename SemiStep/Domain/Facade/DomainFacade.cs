using Domain.Services;

using Shared;
using Shared.Registries;
using Shared.Services;

namespace Domain.Facade;

public sealed class DomainFacade(
	IActionRegistry actionRegistry,
	IPropertyRegistry propertyRegistry,
	IColumnRegistry columnRegistry,
	IGroupRegistry groupRegistry,
	IGridStyleProvider gridStyleProvider,
	RecipeService recipeService)
	: IDisposable
{
	private bool _disposed;

	public RecipeService Recipe => recipeService;

	public IGridStyleProvider GridStyle => gridStyleProvider;

	public void Initialize(AppConfiguration appConfig)
	{
		actionRegistry.Initialize(appConfig.Actions);
		propertyRegistry.Initialize(appConfig.Properties);
		columnRegistry.Initialize(appConfig.Columns);
		groupRegistry.Initialize(appConfig.Groups);
		gridStyleProvider.Initialize(appConfig.GridStyle);

		recipeService.NewRecipe();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}
}
