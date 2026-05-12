using FluentResults;

using SemiStep.Core.Configuration.Dto;
using SemiStep.Core.Shared;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SemiStep.Core.Configuration.Loaders;

internal static class ConnectionLoader
{
	private const string SupportedConnectionFileVersion = "1.0";
	private const string SupportedConnectionProtocol = "1.0";

	private static readonly IDeserializer _deserializer = new DeserializerBuilder()
		.WithNamingConvention(UnderscoredNamingConvention.Instance)
		.IgnoreUnmatchedProperties()
		.Build();

	public static async Task<Result<ConnectionDto?>> LoadAsync(string configDirectory)
	{
		var connectionDir = Path.Combine(configDirectory, "connection");

		if (!Directory.Exists(connectionDir))
		{
			return Result.Fail($"Required config not found: {connectionDir}");
		}

		var filePath = Path.Combine(connectionDir, "connection.yaml");

		if (!File.Exists(filePath))
		{
			return Result.Fail($"Required config not found: {filePath}");
		}

		ConnectionDto? dto;

		try
		{
			var content = await File.ReadAllTextAsync(filePath);
			dto = _deserializer.Deserialize<ConnectionDto?>(content);
		}
		catch (Exception ex)
		{
			return Result.Fail($"Failed to load {Path.GetFileName(filePath)}: {ex.Message}");
		}

		var fileVersionResult = ValidateVersion(
			"connection_file_version",
			dto?.ConnectionFileVersion,
			SupportedConnectionFileVersion);
		if (fileVersionResult.IsFailed)
		{
			return fileVersionResult;
		}

		var protocolResult = ValidateVersion(
			"connection_protocol",
			dto?.ConnectionProtocol,
			SupportedConnectionProtocol);
		if (protocolResult.IsFailed)
		{
			return protocolResult;
		}

		return Result.Ok(dto);
	}

	private static Result ValidateVersion(string fieldName, string? actualValue, string expected)
	{
		if (string.IsNullOrWhiteSpace(actualValue))
		{
			return Result.Fail($"Missing required field '{fieldName}'. Expected: '{expected}'.");
		}

		if (actualValue != expected)
		{
			return Result.Fail($"Unsupported {fieldName}: '{actualValue}'. Expected: '{expected}'.");
		}

		return Result.Ok();
	}
}
