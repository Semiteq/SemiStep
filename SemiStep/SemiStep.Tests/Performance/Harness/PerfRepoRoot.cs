using System;
using System.Collections.Generic;
using System.IO;

namespace SemiStep.Tests.Performance.Harness;

// Locates the repository root by walking up from the test assembly directory (the exe runs from
// SemiStep/Artifacts/bin/.../release/). The root is marked by a `.git` entry or a repo-root global.json;
// the .slnx one level down in SemiStep/ is NOT the marker. Records every directory it visits so the caller
// can report the searched path list on failure. Returns null when no marker is found.
internal static class PerfRepoRoot
{
	public static string? Find(List<string> searched)
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			searched.Add(directory.FullName);
			var gitEntry = Path.Combine(directory.FullName, ".git");
			var globalJson = Path.Combine(directory.FullName, "global.json");
			if (Directory.Exists(gitEntry) || File.Exists(gitEntry) || File.Exists(globalJson))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		return null;
	}
}
