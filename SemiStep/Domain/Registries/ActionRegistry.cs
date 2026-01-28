using Shared.Entities;
using Shared.Registries;

namespace Domain.Registries;

public sealed class ActionRegistry : IActionRegistry
{
	private readonly Dictionary<int, ActionDefinition> _byId = new();
	private readonly Dictionary<string, ActionDefinition> _byName = new(StringComparer.OrdinalIgnoreCase);

	public void Initialize(IReadOnlyDictionary<int, ActionDefinition> actions)
	{
		_byId.Clear();
		_byName.Clear();

		foreach (var (id, action) in actions)
		{
			_byId[id] = action;
			_byName[action.UiName] = action;
		}
	}

	public ActionDefinition GetAction(int id)
	{
		if (!_byId.TryGetValue(id, out var action))
		{
			throw new KeyNotFoundException($"Action with id {id} not found");
		}

		return action;
	}

	public ActionDefinition GetActionByName(string name)
	{
		if (!_byName.TryGetValue(name, out var action))
		{
			throw new KeyNotFoundException($"Action with name '{name}' not found");
		}

		return action;
	}

	public bool ActionExists(short id) => _byId.ContainsKey(id);

	public bool ActionExistsByName(string name) => _byName.ContainsKey(name);

	public IReadOnlyList<ActionDefinition> GetAll() => _byId.Values.ToList();
}
