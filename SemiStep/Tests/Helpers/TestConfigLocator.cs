namespace Tests.Helpers;

public static class TestConfigLocator
{
	public static string GetConfigDirectory(string configName = "Standard")
	{
		var baseDir = AppContext.BaseDirectory;

		for (var i = 0; i < 10 && !string.IsNullOrEmpty(baseDir); i++)
		{
			var probe = Path.Combine(baseDir, "YamlConfigs", configName);
			if (Directory.Exists(probe))
			{
				return probe;
			}

			baseDir = Directory.GetParent(baseDir)?.FullName ?? string.Empty;
		}

		throw new DirectoryNotFoundException(
			$"Config directory '{configName}' not found. Expected 'YamlConfigs/{configName}' in or above the test output directory.");
	}
}
