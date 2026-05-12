namespace SemiStep.Tests.Config.Helpers;

public sealed class TempDirectory : IDisposable
{
	private bool _disposed;

	public TempDirectory()
	{
		Path = System.IO.Path.Combine(
			System.IO.Path.GetTempPath(),
			"SemiStep.ConfigTests",
			Guid.NewGuid().ToString("N"));

		Directory.CreateDirectory(Path);
	}

	public string Path { get; }

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
			// Ignore
		}
	}
}
