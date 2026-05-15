using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.UI.Helpers;

public sealed class UIFixture : IAsyncLifetime
{
	public RecipeSession Session { get; private set; } = null!;
	public PlcLifecycleManager Plc { get; private set; } = null!;
	public RecipeMetadataRegistry RecipeMetadataRegistry { get; private set; } = null!;
	public MessagePanelViewModel MessagePanel { get; private set; } = null!;
	public RecipeCoordinator Coordinator { get; private set; } = null!;

	public async ValueTask InitializeAsync()
	{
		var (services, session, plc) = await CoreTestHelper.BuildAsync("WithGroups");
		Session = session;
		Plc = plc;
		RecipeMetadataRegistry = services.GetRequiredService<RecipeMetadataRegistry>();
		MessagePanel = new MessagePanelViewModel();
		var importedRecipeValidator = services.GetRequiredService<ImportedRecipeValidator>();
		var appConfiguration = services.GetRequiredService<AppConfiguration>();
		var csvService = services.GetRequiredService<CsvService>();
		Coordinator = new RecipeCoordinator(
			Session,
			Plc,
			csvService,
			importedRecipeValidator,
			appConfiguration,
			RecipeMetadataRegistry,
			MessagePanel,
			NullLogger<RecipeCoordinator>.Instance);
		Coordinator.Initialize();
	}

	public ValueTask DisposeAsync()
	{
		Coordinator.Dispose();
		MessagePanel.Dispose();
		return ValueTask.CompletedTask;
	}
}
