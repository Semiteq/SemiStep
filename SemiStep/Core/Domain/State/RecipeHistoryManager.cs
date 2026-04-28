using TypesShared.Core;

namespace Domain.State;

internal sealed class RecipeHistoryManager
{
	private const int MaxHistoryDepth = 100;
	private readonly List<Recipe> _redoStack = new(MaxHistoryDepth);

	private readonly List<Recipe> _undoStack = new(MaxHistoryDepth);

	public bool CanUndo => _undoStack.Count > 0;
	public bool CanRedo => _redoStack.Count > 0;
	public int UndoCount => _undoStack.Count;
	public int RedoCount => _redoStack.Count;

	public void Push(Recipe recipe)
	{
		_redoStack.Clear();

		if (_undoStack.Count >= MaxHistoryDepth)
		{
			_undoStack.RemoveAt(0);
		}

		_undoStack.Add(recipe);
	}

	public Recipe? Undo(Recipe current)
	{
		if (_undoStack.Count == 0)
		{
			return null;
		}

		_redoStack.Add(current);

		var index = _undoStack.Count - 1;
		var previous = _undoStack[index];
		_undoStack.RemoveAt(index);

		return previous;
	}

	public Recipe? Redo(Recipe current)
	{
		if (_redoStack.Count == 0)
		{
			return null;
		}

		_undoStack.Add(current);

		var index = _redoStack.Count - 1;
		var next = _redoStack[index];
		_redoStack.RemoveAt(index);

		return next;
	}

	public void Clear()
	{
		_undoStack.Clear();
		_redoStack.Clear();
	}
}
