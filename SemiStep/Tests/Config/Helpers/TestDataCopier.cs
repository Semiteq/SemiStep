namespace Tests.Config.Helpers;

/// <summary>
/// Copies test data from YamlConfigs directory to temp directories.
/// Supports overlay pattern: copies baseline first, then overlays invalid case files.
/// Also supports standalone pattern for self-contained test cases.
/// </summary>
public static class TestDataCopier
{
	private const string BaselineCaseName = "Standard";
	private const string InvalidCasesFolder = "Invalid";
	private const string StandaloneCasesFolder = "Standalone";

	/// <summary>
	/// Prepares a valid test case by copying it to a temp directory.
	/// </summary>
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

	/// <summary>
	/// Prepares an invalid test case by copying baseline first, then overlaying the invalid case files.
	/// This allows invalid cases to only contain the files that differ from baseline.
	/// </summary>
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

	/// <summary>
	/// Prepares a standalone test case by copying it to a temp directory.
	/// Standalone cases are self-contained (no baseline overlay).
	/// </summary>
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

	/// <summary>
	/// Creates an empty temp directory for tests that build config programmatically.
	/// </summary>
	public static TempDirectory CreateEmptyTempDirectory()
	{
		return new TempDirectory();
	}

	/// <summary>
	/// Ensures required subdirectories exist in temp directory.
	/// </summary>
	public static void EnsureDirectories(TempDirectory tempDir, params string[] subdirectories)
	{
		foreach (var subdir in subdirectories)
		{
			var fullPath = Path.Combine(tempDir.Path, subdir);
			Directory.CreateDirectory(fullPath);
		}
	}

	/// <summary>
	/// Writes YAML content to a file in the temp directory.
	/// </summary>
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
		var baseDir = AppContext.BaseDirectory;

		// Search for YamlConfigs directory in or above the test output directory
		for (var i = 0; i < 10 && !string.IsNullOrEmpty(baseDir); i++)
		{
			var probe = Path.Combine(baseDir, "YamlConfigs", relativePath);
			if (Directory.Exists(probe))
			{
				return probe;
			}

			baseDir = Directory.GetParent(baseDir)?.FullName ?? string.Empty;
		}

		// Fallback: return the expected path (will fail with clear message)
		return Path.Combine(AppContext.BaseDirectory, "YamlConfigs", relativePath);
	}

	private static void CopyDirectory(string sourceDir, string destDir)
	{
		Directory.CreateDirectory(destDir);

		// Copy files
		foreach (var file in Directory.GetFiles(sourceDir))
		{
			var destFile = Path.Combine(destDir, Path.GetFileName(file));
			File.Copy(file, destFile, overwrite: true);
		}

		// Recursively copy subdirectories
		foreach (var subDir in Directory.GetDirectories(sourceDir))
		{
			var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
			CopyDirectory(subDir, destSubDir);
		}
	}
}
