using System.Reactive;

using SemiStep.Core.Recipes;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// View-facing API of the recipe grid. Selection is expressed in step indices only;
/// each implementation owns its native widget, mutation handling, and execution highlighting.
/// </summary>
public interface IRecipeGridSurface : IDisposable
{
	/// <summary>Initial projection from the coordinator's current recipe. Called once at startup.</summary>
	void Initialize();

	int StepCount { get; }

	bool IsReadOnly { get; }

	IReadOnlyList<int> SelectedStepIndices { get; }

	/// <summary>First selected step index, or -1 when the selection is empty.</summary>
	int SelectedStepIndex { get; }

	/// <summary>View to surface: the actual selection produced by the control, mapped to step indices.</summary>
	void UpdateSelection(IReadOnlyList<int> stepIndices);

	/// <summary>Consumer to surface: programmatic selection push. Null clears the selection.</summary>
	void RequestSelection(int? stepIndex);

	/// <summary>Surface to view: the stream <see cref="RequestSelection"/> pushes into.</summary>
	IObservable<int?> SelectionRequests { get; }

	/// <summary>Emits the current value on subscription (WhenAnyValue semantics).</summary>
	IObservable<bool> CanDeleteStep { get; }

	/// <summary>Emits on each <see cref="IsReadOnly"/> false-to-true transition; no replay on subscription.</summary>
	IObservable<Unit> EditorMustClose { get; }

	/// <summary>Selected steps in ascending step-index order.</summary>
	IReadOnlyList<Step> CollectSelectedSteps();
}
