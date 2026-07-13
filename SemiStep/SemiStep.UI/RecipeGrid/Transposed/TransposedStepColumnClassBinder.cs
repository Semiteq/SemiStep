using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace SemiStep.UI.RecipeGrid.Transposed;

// Peer of RecipeRowExecutionClassBinder for ListBoxItem step-column containers. Unlike DataGrid
// rows (whose value store owns the BindClass lifecycle), ListBox containers are recycled through
// ContainerPrepared/ContainerClearing, so bindings are tracked per container and torn down on
// clearing to avoid stacking duplicate class bindings on a recycled container.
internal sealed class TransposedStepColumnClassBinder
{
	private static readonly IReadOnlyList<(string ClassName, string BindingPath)> _classBindings =
	[
		(RowExecutionClasses.CurrentStepClass,
			$"{nameof(StepColumnViewModel.Row)}.{nameof(RecipeRowViewModel.IsCurrentStep)}"),
		(RowExecutionClasses.PastStepClass,
			$"{nameof(StepColumnViewModel.Row)}.{nameof(RecipeRowViewModel.IsPastStep)}"),
		(RowExecutionClasses.ForDepth1Class,
			$"{nameof(StepColumnViewModel.Row)}.{nameof(RecipeRowViewModel.IsForDepth1)}"),
		(RowExecutionClasses.ForDepth2Class,
			$"{nameof(StepColumnViewModel.Row)}.{nameof(RecipeRowViewModel.IsForDepth2)}"),
		(RowExecutionClasses.ForDepth3Class,
			$"{nameof(StepColumnViewModel.Row)}.{nameof(RecipeRowViewModel.IsForDepth3)}"),
	];

	private readonly Dictionary<Control, IReadOnlyList<IDisposable>> _containerBindings = new();

	public void OnContainerPrepared(Control container)
	{
		OnContainerClearing(container);

		_containerBindings[container] = _classBindings
			.Select(pair => container.BindClass(pair.ClassName, new Binding(pair.BindingPath), container))
			.ToList();
	}

	public void OnContainerClearing(Control container)
	{
		if (!_containerBindings.Remove(container, out var bindings))
		{
			return;
		}

		foreach (var binding in bindings)
		{
			binding.Dispose();
		}

		foreach (var (className, _) in _classBindings)
		{
			container.Classes.Remove(className);
		}
	}

	public void Reset()
	{
		foreach (var container in _containerBindings.Keys.ToList())
		{
			OnContainerClearing(container);
		}
	}
}
