using FluentResults;

using SemiStep.Core.Configuration.Dto;

namespace SemiStep.Core.Configuration.Loaders;

internal static class ConnectionLoader
{
	private const string SupportedConnectionFileVersion = "1.0";
	private const string SupportedConnectionProtocol = "1.0";

	public static async Task<Result<ConnectionDto?>> LoadAsync(string configDirectory)
	{
		var connectionDir = Path.Combine(configDirectory, ConfigFolderPolicy.ConnectionFolder);

		if (!Directory.Exists(connectionDir))
		{
			return Result.Fail($"Required config not found: {connectionDir}");
		}

		var filePath = Path.Combine(connectionDir, ConfigFolderPolicy.ConnectionFileName);

		if (!File.Exists(filePath))
		{
			return Result.Fail($"Required config not found: {filePath}");
		}

		ConnectionDto? dto;

		try
		{
			var content = await File.ReadAllTextAsync(filePath);
			dto = YamlConfigDeserializer.Instance.Deserialize<ConnectionDto?>(content);
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
		return protocolResult.IsFailed ? (Result<ConnectionDto?>)protocolResult : Result.Ok(dto);
	}

	private static Result ValidateVersion(string fieldName, string? actualValue, string expected)
	{
		return string.IsNullOrWhiteSpace(actualValue)
			? Result.Fail($"Missing required field '{fieldName}'. Expected: '{expected}'.")
			: actualValue != expected ? Result.Fail($"Unsupported {fieldName}: '{actualValue}'. Expected: '{expected}'.") : Result.Ok();
	}
}
