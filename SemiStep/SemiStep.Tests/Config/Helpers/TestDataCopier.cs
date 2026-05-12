using SemiStep.Tests.Helpers;

namespace SemiStep.Tests.Config.Helpers;

public static class TestDataCopier
{
	private const string BaselineCaseName = "Standard";
	private const string InvalidCasesFolder = "Invalid";
	private const string StandaloneCasesFolder = "Standalone";

	public static TempDirectory PrepareValidCase(string caseName = BaselineCaseName)
	{
		var tempDir = new TempDirectory();
		var sourceDir = GetTestDataPath(caseName);

		if (!Directory.Exists(sourceDir))
		{
			throw new DirectoryNotFoundException($"Valid test case not found: {sourceDir}");
		}

		CopyDirectory(sourceDir, tempDir.Path);

		return tempDir;
	}

	public static TempDirectory PrepareInvalidCase(string invalidCaseName)
	{
		var tempDir = new TempDirectory();

		// Step 1: Copy baseline (Standard)
		var baselineDir = GetTestDataPath(BaselineCaseName);
		if (!Directory.Exists(baselineDir))
		{
			throw new DirectoryNotFoundException($"Baseline test case not found: {baselineDir}");
		}

		CopyDirectory(baselineDir, tempDir.Path);

		// Step 2: Overlay invalid case files
		var invalidDir = GetTestDataPath(Path.Combine(InvalidCasesFolder, invalidCaseName));
		if (!Directory.Exists(invalidDir))
		{
			throw new DirectoryNotFoundException($"Invalid test case not found: {invalidDir}");
		}

		CopyDirectory(invalidDir, tempDir.Path);

		return tempDir;
	}

	public static TempDirectory PrepareStandaloneCase(string caseName)
	{
		var tempDir = new TempDirectory();
		var sourceDir = GetTestDataPath(Path.Combine(StandaloneCasesFolder, caseName));

		if (!Directory.Exists(sourceDir))
		{
			throw new DirectoryNotFoundException($"Standalone test case not found: {sourceDir}");
		}

		CopyDirectory(sourceDir, tempDir.Path);

		return tempDir;
	}

	public static TempDirectory CreateEmptyTempDirectory()
	{
		return new TempDirectory();
	}

	public static void EnsureDirectories(TempDirectory tempDir, params string[] subdirectories)
	{
		foreach (var subdir in subdirectories)
		{
			var fullPath = Path.Combine(tempDir.Path, subdir);
			Directory.CreateDirectory(fullPath);
		}
	}

	public static void WriteYaml(TempDirectory tempDir, string relativePath, string content)
	{
		var fullPath = Path.Combine(tempDir.Path, relativePath);
		var directory = Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(fullPath, content);
	}

	private static string GetTestDataPath(string relativePath)
	{
		return TestConfigLocator.GetConfigDirectory(relativePath);
	}

	private static void CopyDirectory(string sourceDir, string destDir)
	{
		Directory.CreateDirectory(destDir);

		foreach (var file in Directory.GetFiles(sourceDir))
		{
			var destFile = Path.Combine(destDir, Path.GetFileName(file));
			File.Copy(file, destFile, overwrite: true);
		}

		foreach (var subDir in Directory.GetDirectories(sourceDir))
		{
			var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
			CopyDirectory(subDir, destSubDir);
		}
	}
}
