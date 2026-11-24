namespace SemiStep.Core.Recipes;

internal sealed class RecipeHistory
{
	private const int MaxDepth = 100;

	private readonly List<Recipe> _undoStack = [with(MaxDepth)];
	private readonly List<Recipe> _redoStack = [with(MaxDepth)];

	public int UndoCount => _undoStack.Count;

	public bool CanRedo => _redoStack.Count > 0;

	public Recipe? PeekUndo()
	{
		return _undoStack.Count > 0 ? _undoStack[^1] : null;
	}

	public Recipe? PeekRedo()
	{
		return _redoStack.Count > 0 ? _redoStack[^1] : null;
	}

	public void Push(Recipe recipe)
	{
		_redoStack.Clear();

		if (_undoStack.Count >= MaxDepth)
		{
			_undoStack.RemoveAt(0);
		}

		_undoStack.Add(recipe);
	}

	public void CommitUndo(Recipe current)
	{
		_redoStack.Add(current);
		_undoStack.RemoveAt(_undoStack.Count - 1);
	}

	public void CommitRedo(Recipe current)
	{
		_undoStack.Add(current);
		_redoStack.RemoveAt(_redoStack.Count - 1);
	}

	public void Clear()
	{
		_undoStack.Clear();
		_redoStack.Clear();
	}
}
