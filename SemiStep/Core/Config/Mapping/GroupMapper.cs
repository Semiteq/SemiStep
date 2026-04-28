using TypesShared.Config;

namespace Config.Mapping;

internal static class GroupMapper
{
	public static IReadOnlyDictionary<string, GroupDefinition> Map(
		Dictionary<string, Dictionary<int, string>> groupsDto)
	{
		var result = new Dictionary<string, GroupDefinition>(StringComparer.OrdinalIgnoreCase);

		foreach (var (groupId, items) in groupsDto)
		{
			var definition = new GroupDefinition(
				GroupId: groupId,
				Items: items.AsReadOnly());

			result[groupId] = definition;
		}

		return result;
	}
}
