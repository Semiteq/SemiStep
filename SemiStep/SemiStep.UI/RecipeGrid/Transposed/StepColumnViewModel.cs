using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

public sealed class StepColumnViewModel : IDisposable
{
	private readonly Lazy<IReadOnlyList<ParameterCellViewModel>> _cells;

	public StepColumnViewModel(
		int stepNumber,
		Step step,
		ActionDefinition action,
		RecipeMetadataRegistry recipeMetadataRegistry,
		IReadOnlyList<ParameterDescriptor> parameterDescriptors,
		Func<RecipeRowViewModel, ParameterDescriptor, ParameterCellViewModel> cellFactory)
	{
		var inapplicableColumns = RecipeRowViewModel.BuildInapplicableColumns(action, step, recipeMetadataRegistry);
		Row = new RecipeRowViewModel(stepNumber, step, action, recipeMetadataRegistry, inapplicableColumns);

		// A column never scrolled to or keyboard-traversed never builds its per-parameter cell VMs.
		// Cells is UI-thread-only, so the default locking Lazy mode is unnecessary overhead.
		_cells = new Lazy<IReadOnlyList<ParameterCellViewModel>>(
			() => parameterDescriptors.Select(descriptor => cellFactory(Row, descriptor)).ToList(),
			LazyThreadSafetyMode.None);
	}

	public RecipeRowViewModel Row { get; }

	public IReadOnlyList<ParameterCellViewModel> Cells => _cells.Value;

	public void Dispose()
	{
		if (_cells.IsValueCreated)
		{
			foreach (var cell in _cells.Value)
			{
				cell.Dispose();
			}
		}

		Row.Dispose();
	}
}
