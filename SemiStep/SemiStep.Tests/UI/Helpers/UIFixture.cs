using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Clipboard;
using SemiStep.Core.Recipes.Helpers;
using SemiStep.Core.Recipes.Import;
using SemiStep.Tests.Core.Helpers;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;
using SemiStep.UI.RecipeGrid;

using Xunit;

namespace SemiStep.Tests.SemiStep.UI.Helpers;

public sealed class UIFixture : IAsyncLifetime
{
	public RecipeWorkspace Workspace { get; private set; } = null!;
	public RecipeEditor Editor { get; private set; } = null!;
	public PlcLifecycleManager Plc { get; private set; } = null!;
	public RecipeMetadataRegistry RecipeMetadataRegistry { get; private set; } = null!;
	public MessagePanelViewModel MessagePanel { get; private set; } = null!;
	public RecipeQueryService QueryService { get; private set; } = null!;
	public RecipeMutationCoordinator Coordinator { get; private set; } = null!;
	public RecipeGridViewModel Grid { get; private set; } = null!;

	public async Task InitializeAsync()
	{
		var (services, workspace, editor, plc) = await CoreTestHelper.BuildAsync("WithGroups");
		Workspace = workspace;
		Editor = editor;
		Plc = plc;
		RecipeMetadataRegistry = services.GetRequiredService<RecipeMetadataRegistry>();
		MessagePanel = new MessagePanelViewModel();
		var clipboardSerializer = services.GetRequiredService<ClipboardSerializer>();
		var importedRecipeValidator = services.GetRequiredService<ImportedRecipeValidator>();
		QueryService = new RecipeQueryService(Workspace, plc, clipboardSerializer, importedRecipeValidator, RecipeMetadataRegistry);
		var appConfiguration = services.GetRequiredService<AppConfiguration>();
		var csvService = services.GetRequiredService<CsvService>();
		Coordinator = new RecipeMutationCoordinator(
			Workspace,
			Editor,
			Plc,
			csvService,
			importedRecipeValidator,
			appConfiguration,
			QueryService,
			MessagePanel,
			NullLogger<RecipeMutationCoordinator>.Instance);
		Coordinator.Initialize();
		Grid = new RecipeGridViewModel(Coordinator, RecipeMetadataRegistry, MessagePanel);
		Grid.Initialize();
	}

	public Task DisposeAsync()
	{
		Grid.Dispose();
		Coordinator.Dispose();
		MessagePanel.Dispose();
		return Task.CompletedTask;
	}
}
