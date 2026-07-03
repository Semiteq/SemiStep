using System.Globalization;
using System.Text.RegularExpressions;

using FluentAssertions;

using FluentResults;

using SemiStep.Core.Configuration;
using SemiStep.Core.Configuration.Loaders;
using SemiStep.Core.Configuration.Mapping;
using SemiStep.Tests.Config.Helpers;
using SemiStep.Tests.Helpers;

using Xunit;

namespace SemiStep.Tests.Core.Configuration;

[Trait("Component", "Config")]
[Trait("Category", "Unit")]
[Trait("Area", "GridStyleWriter")]
public sealed class GridStyleWriterTests
{
	[Theory]
	[InlineData("MBE")]
	[InlineData("MOCVD")]
	[InlineData("RIE")]
	public async Task Save_SemanticRoundTrip_PreservesRecord(string equipment)
	{
		using var tempDir = CopyShippedConfig(equipment);

		var original = await LoadOptions(tempDir.Path);

		new GridStyleWriter().Save(tempDir.Path, original).IsSuccess.Should().BeTrue();

		var reloaded = await LoadOptions(tempDir.Path);

		reloaded.Should().Be(original);
	}

	[Theory]
	[InlineData("MBE")]
	[InlineData("MOCVD")]
	public async Task Save_PreservesLeadingCommentBlock_WhenHeaderPresent(string equipment)
	{
		using var tempDir = CopyShippedConfig(equipment);
		var filePath = GridStyleFilePath(tempDir.Path);

		var token = TestContext.Current.CancellationToken;
		var originalHeader = LeadingCommentBlock(await File.ReadAllTextAsync(filePath, token));
		originalHeader.Should().NotBeEmpty();
		var options = await LoadOptions(tempDir.Path);

		new GridStyleWriter().Save(tempDir.Path, options).IsSuccess.Should().BeTrue();

		var writtenHeader = LeadingCommentBlock(await File.ReadAllTextAsync(filePath, token));
		writtenHeader.Should().Be(originalHeader);
		writtenHeader.Should().Contain("# Grid Style Configuration");
	}

	[Fact]
	public async Task Save_InjectsNoHeader_WhenSourceHasNone()
	{
		using var tempDir = CopyShippedConfig("RIE");
		var filePath = GridStyleFilePath(tempDir.Path);
		var token = TestContext.Current.CancellationToken;

		LeadingCommentBlock(await File.ReadAllTextAsync(filePath, token)).Should().BeEmpty();
		var options = await LoadOptions(tempDir.Path);

		new GridStyleWriter().Save(tempDir.Path, options).IsSuccess.Should().BeTrue();

		var written = (await File.ReadAllTextAsync(filePath, token)).Replace("\r\n", "\n");
		var firstNonBlank = written.Split('\n').First(line => line.TrimStart().Length > 0);
		firstNonBlank.Should().Be("fonts:");
	}

	[Fact]
	public async Task Save_EmitsColorScalarsDoubleQuoted()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var filePath = GridStyleFilePath(tempDir.Path);
		var options = await LoadOptions(tempDir.Path);

		new GridStyleWriter().Save(tempDir.Path, options).IsSuccess.Should().BeTrue();

		var content = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
		content.Should().Contain("\"#");
		content.Should().Contain($"\"{options.SelectionBackgroundColor}\"");

		var unquotedHex = Regex.Matches(content, @":\s*#[0-9A-Fa-f]+");
		unquotedHex.Should().BeEmpty(
			"every color property must carry the double-quoted scalar style; an unquoted hex means a "
			+ "DTO property is missing [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]");
	}

	[Fact]
	public async Task Save_WhenMoveFails_FailsAndLeavesOriginalIntactWithNoTempOrphan()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var filePath = GridStyleFilePath(tempDir.Path);
		var uiDir = Path.Combine(tempDir.Path, "ui");
		var token = TestContext.Current.CancellationToken;

		var before = await File.ReadAllBytesAsync(filePath, token);
		var options = await LoadOptions(tempDir.Path) with { CellFontSize = 20 };

		Result result;

		// Hold an exclusive lock on the target so the atomic File.Move cannot replace it.
		using (var locking = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
		{
			result = new GridStyleWriter().Save(tempDir.Path, options);
		}

		result.IsFailed.Should().BeTrue();

		var after = await File.ReadAllBytesAsync(filePath, token);
		after.Should().Equal(before);

		Directory.GetFiles(uiDir, "*.tmp").Should().BeEmpty();
	}

	[Fact]
	public async Task Save_WritesLfLineEndingsWithoutBom()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var filePath = GridStyleFilePath(tempDir.Path);
		var options = await LoadOptions(tempDir.Path);

		new GridStyleWriter().Save(tempDir.Path, options).IsSuccess.Should().BeTrue();

		var bytes = await File.ReadAllBytesAsync(filePath, TestContext.Current.CancellationToken);
		bytes.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF });
		bytes.Should().NotContain((byte)'\r');
	}

	[Fact]
	public async Task Save_UnderCommaLocale_EmitsInvariantDecimals()
	{
		using var tempDir = CopyShippedConfig("MBE");
		var filePath = GridStyleFilePath(tempDir.Path);
		var options = await LoadOptions(tempDir.Path) with { CellPaddingLeft = 6.5 };

		var previousCulture = CultureInfo.CurrentCulture;
		try
		{
			CultureInfo.CurrentCulture = new CultureInfo("de-DE");
			new GridStyleWriter().Save(tempDir.Path, options).IsSuccess.Should().BeTrue();
		}
		finally
		{
			CultureInfo.CurrentCulture = previousCulture;
		}

		var content = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
		content.Should().Contain("cell_padding_left: 6.5");
		content.Should().NotContain("6,5");
	}

	private static async Task<GridStyleOptions> LoadOptions(string configDir)
	{
		var result = await GridStyleLoader.LoadAsync(configDir);
		result.IsSuccess.Should().BeTrue();
		return GridStyleMapper.Map(result.Value);
	}

	private static TempDirectory CopyShippedConfig(string equipment)
	{
		var source = ShippedConfigLocator.GetConfigDirectory(equipment);
		var tempDir = new TempDirectory();
		var uiDir = Path.Combine(tempDir.Path, "ui");
		Directory.CreateDirectory(uiDir);
		File.Copy(
			Path.Combine(source, "ui", "grid_style.yaml"),
			Path.Combine(uiDir, "grid_style.yaml"),
			overwrite: true);
		return tempDir;
	}

	private static string GridStyleFilePath(string configDir)
	{
		return Path.Combine(configDir, "ui", "grid_style.yaml");
	}

	private static string LeadingCommentBlock(string content)
	{
		var lines = content.Replace("\r\n", "\n").Split('\n');
		var headerLines = new List<string>();
		foreach (var line in lines)
		{
			var trimmed = line.TrimStart();
			if (trimmed.Length == 0 || trimmed.StartsWith('#'))
			{
				headerLines.Add(line);
			}
			else
			{
				break;
			}
		}

		return string.Join('\n', headerLines).TrimEnd('\n');
	}
}
