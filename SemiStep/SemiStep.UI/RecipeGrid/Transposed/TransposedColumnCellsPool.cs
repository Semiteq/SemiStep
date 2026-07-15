using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid.Transposed;

// A small view-owned pool of reusable column-cells presenters. The transposed ListBox recycles its
// containers by detaching and rebuilding their ItemTemplate content, so a presenter placed inside that
// content would be rebuilt on every scroll. The pool holds presenters OUTSIDE that recycled content and
// hands one to each realized column (via TransposedColumnCellsHost), so the built cell subtrees are
// reused across recycling instead of rebuilt. The pool only ever grows to the realized-container count
// (a viewport of columns), so never-scrolled columns cost nothing.
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
