using FluentAssertions;

using SemiStep.Core.Configuration.Loaders;
using SemiStep.Tests.Config.Helpers;

using Xunit;

namespace SemiStep.Tests.Config.Integration.Loaders;

[Trait("Category", "Integration")]
[Trait("Component", "Config")]
[Trait("Area", "ConnectionVersion")]
public sealed class ConnectionLoaderVersionTests
{
	private const string ValidConnectionYaml = """
		connection_file_version: "1.0"
		connection_protocol: "1.0"
		ip: "192.168.0.1:102"
		""";

	[Theory]
	[InlineData(
		"""
		connection_file_version: "2.0"
		connection_protocol: "1.0"
		""",
		"Unsupported connection_file_version",
		"'2.0'")]
	[InlineData(
		"""
		connection_file_version: "1.0"
		connection_protocol: "S7"
		""",
		"Unsupported connection_protocol",
		"'S7'")]
	public async Task ConnectionLoader_RejectsUnsupportedValue(
		string yaml,
		string expectedPrefix,
		string expectedActualValue)
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(tempDir, Path.Combine("connection", "connection.yaml"), yaml);

		var result = await ConnectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains(expectedPrefix, StringComparison.Ordinal) &&
			e.Message.Contains(expectedActualValue, StringComparison.Ordinal) &&
			e.Message.Contains("'1.0'", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData(
		"""
		connection_protocol: "1.0"
		""",
		"connection_file_version")]
	[InlineData(
		"""
		connection_file_version: "1.0"
		""",
		"connection_protocol")]
	public async Task ConnectionLoader_RejectsMissingRequiredField(string yaml, string missingFieldName)
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(tempDir, Path.Combine("connection", "connection.yaml"), yaml);

		var result = await ConnectionLoader.LoadAsync(tempDir.Path);

		result.IsFailed.Should().BeTrue();
		result.Errors.Should().Contain(e =>
			e.Message.Contains("Missing required field", StringComparison.Ordinal) &&
			e.Message.Contains(missingFieldName, StringComparison.Ordinal));
	}

	[Fact]
	public async Task ConnectionLoader_AcceptsSupportedVersions()
	{
		using var tempDir = TestDataCopier.CreateEmptyTempDirectory();
		TestDataCopier.WriteYaml(
			tempDir,
			Path.Combine("connection", "connection.yaml"),
			ValidConnectionYaml);

		var result = await ConnectionLoader.LoadAsync(tempDir.Path);

		result.IsSuccess.Should().BeTrue(result.IsFailed
			? string.Join("; ", result.Errors.Select(e => e.Message))
			: string.Empty);
		result.Value.Should().NotBeNull();
		result.Value!.ConnectionFileVersion.Should().Be("1.0");
		result.Value.ConnectionProtocol.Should().Be("1.0");
	}
}
