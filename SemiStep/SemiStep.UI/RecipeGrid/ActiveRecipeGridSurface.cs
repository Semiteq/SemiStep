using System.Reactive;
using System.Reactive.Linq;

using ReactiveUI;

using SemiStep.Core.Configuration;
using SemiStep.Core.Recipes;

using SemiStep.UI.RecipeGrid.Transposed;

namespace SemiStep.UI.RecipeGrid;

/// <summary>
/// Delegating router over the two orientation surfaces and the single owner of the
/// orientation choice. Interface consumers survive orientation flips because every
/// member tracks the active surface and the observables re-subscribe across swaps.
/// </summary>
public class ActiveRecipeGridSurface : ReactiveObject, IRecipeGridSurface
{
	private GridOrientation _orientation;

	public ActiveRecipeGridSurface(
		CanonicalRecipeGridSurface canonicalSurface,
		TransposedRecipeGridSurface transposedSurface,
		GridStyleOptions gridStyle)
	{
		CanonicalSurface = canonicalSurface;
		TransposedSurface = transposedSurface;
		_orientation = gridStyle.Orientation;

		SelectionRequests = this
			.WhenAnyValue(x => x.Orientation)
			.Select(orientation => SurfaceFor(orientation).SelectionRequests)
			.Switch();

		CanDeleteStep = this
			.WhenAnyValue(x => x.Orientation)
			.Select(orientation => SurfaceFor(orientation).CanDeleteStep)
			.Switch()
			.DistinctUntilChanged();

		EditorMustClose = this
			.WhenAnyValue(x => x.Orientation)
			.Select(orientation => SurfaceFor(orientation).EditorMustClose)
			.Switch();
	}

	public CanonicalRecipeGridSurface CanonicalSurface { get; }

	public TransposedRecipeGridSurface TransposedSurface { get; }

	public GridOrientation Orientation
	{
		get => _orientation;
		private set => this.RaiseAndSetIfChanged(ref _orientation, value);
	}

	public IObservable<int?> SelectionRequests { get; }

	public IObservable<bool> CanDeleteStep { get; }

	public IObservable<Unit> EditorMustClose { get; }

	public int StepCount => ActiveSurface.StepCount;

	public bool IsReadOnly => ActiveSurface.IsReadOnly;

	public IReadOnlyList<int> SelectedStepIndices => ActiveSurface.SelectedStepIndices;

	public int SelectedStepIndex => ActiveSurface.SelectedStepIndex;

	private IRecipeGridSurface ActiveSurface => SurfaceFor(Orientation);

	public void ToggleOrientation()
	{
		var nextOrientation = Orientation == GridOrientation.RowsAsSteps
			? GridOrientation.ColumnsAsSteps
			: GridOrientation.RowsAsSteps;

		// Transfer before the flip so subscribers that re-attach on the orientation change
		// observe the incoming surface with the carried-over selection already in place.
		SurfaceFor(nextOrientation).UpdateSelection(ActiveSurface.SelectedStepIndices);
		Orientation = nextOrientation;
	}

	// Fans out to both surfaces: Mutated is a no-replay event, so a surface left
	// uninitialized would stay blank until the next RecipeReplaced.
	public void Initialize()
	{
		CanonicalSurface.Initialize();
		TransposedSurface.Initialize();
	}

	public void UpdateSelection(IReadOnlyList<int> stepIndices)
	{
		ActiveSurface.UpdateSelection(stepIndices);
	}

	public void RequestSelection(int? stepIndex)
	{
		ActiveSurface.RequestSelection(stepIndex);
	}

	public IReadOnlyList<Step> CollectSelectedSteps()
	{
		return ActiveSurface.CollectSelectedSteps();
	}

	// The concrete surfaces are container-owned singletons; the container disposes them.
	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}

	private IRecipeGridSurface SurfaceFor(GridOrientation orientation)
	{
		return orientation == GridOrientation.ColumnsAsSteps
			? TransposedSurface
			: CanonicalSurface;
	}
}
