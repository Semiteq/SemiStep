namespace SemiStep.Tests.Helpers;

/// <summary>
/// Locates a shipped equipment config under the repository's <c>ConfigFiles/&lt;name&gt;</c>
/// directory (e.g. RIE, MOCVD, MBE) by walking up from the test output directory. Used by
/// tests that must validate the real shipped config rather than a synthetic fixture.
/// </summary>
public static class ShippedConfigLocator
{
	public static string GetConfigDirectory(string equipmentName)
	{
		ArgumentException.ThrowIfNullOrEmpty(equipmentName);

		var baseDir = AppContext.BaseDirectory;

		for (var i = 0; i < 12 && !string.IsNullOrEmpty(baseDir); i++)
		{
			var probe = Path.Combine(baseDir, "ConfigFiles", equipmentName);
			if (Directory.Exists(probe))
			{
				return probe;
			}

			baseDir = Directory.GetParent(baseDir)?.FullName ?? string.Empty;
		}

		throw new DirectoryNotFoundException(
			$"Shipped config '{equipmentName}' not found. Expected 'ConfigFiles/{equipmentName}' "
			+ "in or above the test output directory.");
	}
}
