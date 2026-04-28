using Config.Dto;

using FluentResults;

using TypesShared.Results;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Config.Loaders;

internal static class ConnectionLoader
{
	private static readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	public static async Task<Result<ConnectionDto?>> LoadAsync(string configDirectory)
	{
		var connectionDir = Path.Combine(configDirectory, "connection");

		if (!Directory.Exists(connectionDir))
		{
			return Result.Ok()
				.WithWarning($"Connection directory not found, using default PLC settings: {connectionDir}");
		}

		var filePath = Path.Combine(connectionDir, "connection.yaml");

		if (!File.Exists(filePath))
		{
			return Result.Ok()
				.WithWarning($"Connection file not found, using defaults: {filePath}");
		}

		try
		{
			var content = await File.ReadAllTextAsync(filePath);

			return Result.Ok(_deserializer.Deserialize<ConnectionDto?>(content));
		}
		catch (Exception ex)
		{
			return Result.Ok()
				.WithWarning($"Failed to parse connection file, using defaults: {ex.Message}");
		}
	}
}
