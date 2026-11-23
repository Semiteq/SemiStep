using FluentResults;

namespace SemiStep.Core.Configuration.Loaders;

internal sealed class UnexpectedFileError(string fileName, string folderName, IReadOnlyList<string> acceptedNames)
	: Error($"Unexpected file '{fileName}' in {folderName}; accepted: {string.Join(", ", acceptedNames)}")
{
	public string FileName { get; } = fileName;

	public string FolderName { get; } = folderName;

	public IReadOnlyList<string> AcceptedNames { get; } = acceptedNames;
}
