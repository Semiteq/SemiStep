using System.Collections.Frozen;

using FluentResults;

namespace SemiStep.Core.Configuration.Loaders;

internal static class ConfigFolderPolicy
{
	public const string ActionsFolder = "actions";
	public const string ColumnsFolder = "columns";
	public const string GroupsFolder = "groups";
	public const string PropertiesFolder = "properties";
	public const string ConnectionFolder = "connection";
	public const string UiFolder = "ui";

	public const string ConnectionFileName = "connection.yaml";
	public const string GridStyleFileName = "grid_style.yaml";
	public const string AppFileName = "app.yaml";

	private const string YamlExtension = ".yaml";
	private const string YmlExtension = ".yml";

	private static readonly IReadOnlyList<string> _sectionFolders =
		[ActionsFolder, ColumnsFolder, GroupsFolder, PropertiesFolder, ConnectionFolder, UiFolder];

	private static readonly IReadOnlyList<string> _mergeFolderAcceptedNames = [$"*{YamlExtension}"];

	private static readonly FrozenDictionary<string, IReadOnlyList<string>> _namedFileFolders =
		new Dictionary<string, IReadOnlyList<string>>
		{
			[ConnectionFolder] = [ConnectionFileName],
			[UiFolder] = [GridStyleFileName, AppFileName],
		}.ToFrozenDictionary();

	public static IReadOnlyList<string> GetYamlFilesToRead(string sectionDirectory)
	{
		return [.. Directory.GetFiles(sectionDirectory)
			.Where(file => HasExtension(file, YamlExtension))
			.OrderBy(file => file, StringComparer.Ordinal)];
	}

	public static Result Scan(string configDirectory)
	{
		var errors = new List<Error>();

		foreach (var folderName in _sectionFolders)
		{
			var folderPath = Path.Combine(configDirectory, folderName);
			if (!Directory.Exists(folderPath))
			{
				continue;
			}

			errors.AddRange(FindRejectedFiles(folderPath, folderName));
		}

		return errors.Count == 0 ? Result.Ok() : Result.Fail(errors);
	}

	private static IEnumerable<Error> FindRejectedFiles(string folderPath, string folderName)
	{
		var fileNames = Directory.GetFiles(folderPath)
			.Select(file => Path.GetFileName(file))
			.Order(StringComparer.Ordinal);

		if (_namedFileFolders.TryGetValue(folderName, out var acceptedNames))
		{
			return fileNames
				.Where(fileName => IsYaml(fileName) && !acceptedNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
				.Select(fileName => new UnexpectedFileError(fileName, folderName, acceptedNames));
		}

		return fileNames
			.Where(fileName => HasExtension(fileName, YmlExtension))
			.Select(fileName => new UnexpectedFileError(fileName, folderName, _mergeFolderAcceptedNames));
	}

	private static bool IsYaml(string file)
	{
		return HasExtension(file, YamlExtension) || HasExtension(file, YmlExtension);
	}

	private static bool HasExtension(string file, string extension)
	{
		return string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase);
	}
}
