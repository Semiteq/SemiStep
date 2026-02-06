using Core.Analysis;
using Core.Entities;

namespace Domain.State;

public sealed class RecipeStateManager
{
	private Recipe _lastValid = Recipe.Empty;

	public event Action<Recipe>? RecipeChanged;

	public RecipeSnapshot? LastSnapshot { get; private set; }
	public Recipe Current => LastSnapshot?.Recipe ?? Recipe.Empty;
	public bool IsDirty { get; private set; }
	public bool IsValid => LastSnapshot?.IsValid ?? false;

	public void Update(RecipeSnapshot snapshot)
	{
		LastSnapshot = snapshot;
		IsDirty = true;

		if (snapshot.IsValid)
		{
			_lastValid = snapshot.Recipe;
			RecipeChanged?.Invoke(_lastValid);
		}
	}

	public void MarkSaved()
	{
		IsDirty = false;
	}

	public void Reset()
	{
		LastSnapshot = RecipeSnapshot.Empty;
		_lastValid = Recipe.Empty;
		IsDirty = false;
	}
}
