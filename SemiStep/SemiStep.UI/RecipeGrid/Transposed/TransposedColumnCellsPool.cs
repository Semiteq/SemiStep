using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

internal sealed class TransposedColumnCellsPool(
	IReadOnlyList<ParameterDescriptor> descriptors,
	TransposedCellTemplateFactory cellFactory,
	double cellHeight)
{
	private readonly Stack<TransposedColumnCellsPresenter> _idle = new();

	public TransposedColumnCellsPresenter Acquire()
	{
		return _idle.Count > 0
			? _idle.Pop()
			: new TransposedColumnCellsPresenter(descriptors, cellFactory, cellHeight);
	}

	public void Return(TransposedColumnCellsPresenter presenter)
	{
		_idle.Push(presenter);
	}
}
