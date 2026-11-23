using FluentResults;

namespace SemiStep.Core.Configuration.Loaders;

internal static class GroupsSectionLoader
{
	public static async Task<Result<Dictionary<string, Dictionary<int, string>>>> LoadAsync(
		string configDirectory)
	{
		var loaded = await MergeSectionLoader
			.LoadAsync<string, Dictionary<int, string>, KeyValuePair<string, Dictionary<int, string>>>(
				configDirectory,
				ConfigFolderPolicy.GroupsFolder,
				StringComparer.OrdinalIgnoreCase,
				(groupId, items, _) => DropEmptyItems(groupId, items));

		return loaded.Map(groups => groups.ToDictionary(StringComparer.OrdinalIgnoreCase));
	}

	private static Result<KeyValuePair<string, Dictionary<int, string>>> DropEmptyItems(
		string groupId,
		Dictionary<int, string> items)
	{
		var validItems = items
			.Where(kv => !string.IsNullOrEmpty(kv.Value))
			.ToDictionary(kv => kv.Key, kv => kv.Value);

		return Result.Ok(KeyValuePair.Create(groupId, validItems));
	}
}
