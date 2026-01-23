using Shared.Entities;

namespace Shared.Registries;

public interface IGroupRegistry
{
	GroupDefinition GetGroup(string groupId);

	bool GroupExists(string groupId);

	IReadOnlyList<GroupDefinition> GetAll();
}
