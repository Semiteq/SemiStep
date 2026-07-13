using System.Collections.ObjectModel;

using Microsoft.Extensions.Logging;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

using SemiStep.UI.Coordinator;
using SemiStep.UI.MessageService;

namespace SemiStep.UI.RecipeGrid.Transposed;

public class TransposedRecipeGridSurface(
	RecipeCoordinator coordinator,
	RecipeMetadataRegistry recipeMetadataRegistry,
	GridStyleOptions gridStyle,
	MessagePanelViewModel messagePanel,
	ChangedCellClickAwayBroadcaster changedCellClickAwayBroadcaster,
	ILogger<TransposedRecipeGridSurface> logger)
	: RecipeGridSurfaceBase<StepColumnViewModel>(
		coordinator, recipeMetadataRegistry, messagePanel, changedCellClickAwayBroadcaster, logger)
{
	// Field initializers run before the base constructor subscribes to coordinator events,
	// so a callback can never observe a half-constructed surface.
	private readonly ParameterCellViewModelFactory _parameterCellViewModelFactory = new(recipeMetadataRegistry);

	public GridStyleOptions GridStyle { get; } = gridStyle;

	public IReadOnlyList<ParameterDescriptor> ParameterDescriptors { get; } =
		ParameterDescriptor.BuildFromRegistry(recipeMetadataRegistry);

	public ObservableCollection<StepColumnViewModel> StepColumns => Items;

	protected override RecipeRowViewModel RowOf(StepColumnViewModel item)
	{
		return item.Row;
	}

	protected override StepColumnViewModel CreateItem(int stepNumber, Step step, ActionDefinition action)
	{
		return new StepColumnViewModel(
			stepNumber,
			step,
			action,
			RecipeMetadataRegistry,
			ParameterDescriptors,
			_parameterCellViewModelFactory.Create);
	}
}
