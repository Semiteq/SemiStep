namespace Tests.Config.Helpers;

/// <summary>
/// IDisposable wrapper around a temporary directory.
/// Creates a unique temp directory on construction, deletes it on disposal.
/// </summary>
public sealed class TempDirectory : IDisposable
{
	private bool _disposed;

	public string Path { get; }

	public TempDirectory()
	{
		Path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			"SemiStep.ConfigTests",
			Guid.NewGuid().ToString("N"));

		Directory.CreateDirectory(Path);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		TryDeleteDirectory(Path);
	}

	private static void TryDeleteDirectory(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch
		{
			// Swallow exceptions during cleanup - temp files will be cleaned up eventually
		}
	}
}
