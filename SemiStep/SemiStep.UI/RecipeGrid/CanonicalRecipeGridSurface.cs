using System.Collections.ObjectModel;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Recipes;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeGrid;

public class CanonicalRecipeGridSurface(
	RecipeCoordinator coordinator,
	RecipeMetadataRegistry recipeMetadataRegistry,
	ColumnBuilder columnBuilder,
	MessagePanelViewModel messagePanel,
	ChangedCellClickAwayBroadcaster changedCellClickAwayBroadcaster,
	ILogger<CanonicalRecipeGridSurface> logger)
	: RecipeGridSurfaceBase<RecipeRowViewModel>(
		coordinator, recipeMetadataRegistry, messagePanel, changedCellClickAwayBroadcaster, logger)
{
	public ColumnBuilder ColumnBuilder { get; } = columnBuilder;

	public ObservableCollection<RecipeRowViewModel> RecipeRows => Items;

	protected override RecipeRowViewModel RowOf(RecipeRowViewModel item)
	{
		return item;
	}

	protected override RecipeRowViewModel CreateItem(int stepNumber, Step step, ActionDefinition action)
	{
		var inapplicableColumns = RecipeRowViewModel.BuildInapplicableColumns(action, step, RecipeMetadataRegistry);

		return new RecipeRowViewModel(
			stepNumber,
			step,
			action,
			RecipeMetadataRegistry,
			inapplicableColumns);
	}
}
