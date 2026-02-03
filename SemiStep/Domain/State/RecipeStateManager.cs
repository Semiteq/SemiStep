using Core.Analysis;
using Core.Entities;

namespace Domain.State;

public sealed class RecipeStateManager
{
	private Recipe _lastValid = Recipe.Empty;

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
		}
	}

	public void Load(RecipeSnapshot snapshot)
	{
		LastSnapshot = snapshot;
		IsDirty = false;

		if (snapshot.IsValid)
		{
			_lastValid = snapshot.Recipe;
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
