using ClipBoard;

using Csv;

using Domain;
using Domain.Facade;
using Domain.Helpers;

using Microsoft.Extensions.DependencyInjection;

using Tests.Core.Helpers;
using Tests.Helpers;

using TypesShared.Config;

using UI.Coordinator;
using UI.MessageService;
using UI.RecipeGrid;

using Xunit;

namespace Tests.UI.Helpers;

public sealed class UIFixture : IAsyncLifetime
{
	public RecipeWorkspace Workspace { get; private set; } = null!;
	public RecipeEditor Editor { get; private set; } = null!;
	public PlcLifecycleManager Plc { get; private set; } = null!;
	public ConfigRegistry ConfigRegistry { get; private set; } = null!;
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
		ConfigRegistry = services.GetRequiredService<ConfigRegistry>();
		MessagePanel = new MessagePanelViewModel();
		var clipboardService = services.GetRequiredService<ClipboardService>();
		var importedRecipeValidator = services.GetRequiredService<ImportedRecipeValidator>();
		QueryService = new RecipeQueryService(Workspace, plc, clipboardService, importedRecipeValidator, ConfigRegistry);
		var appConfiguration = services.GetRequiredService<AppConfiguration>();
		var csvService = services.GetRequiredService<CsvService>();
		Coordinator = new RecipeMutationCoordinator(
			Workspace,
			Editor,
			Plc,
			csvService,
			clipboardService,
			importedRecipeValidator,
			appConfiguration,
			QueryService,
			MessagePanel);
		Coordinator.Initialize();
		Grid = new RecipeGridViewModel(Coordinator, ConfigRegistry, MessagePanel);
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
