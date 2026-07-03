using System.Buffers.Binary;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "WriteTransaction")]
[Trait("Category", "Unit")]
public sealed class PlcTransactionExecutorTests
{
	// Layout where CapacityOffset=0 (4 bytes) and CurrentSizeOffset=4 (4 bytes), so the
	// 8-byte header leaves enough room for ReadUInt32BigEndian at both offsets.
	private static DataDbLayout BuildTestDataDbLayout(int dbNumber)
	{
		return new(DbNumber: dbNumber, CapacityOffset: 0, CurrentSizeOffset: 4, DataStartOffset: 8);
	}

	private static PlcConfiguration BuildTestConfiguration(ManagingDbLayout? managingDb = null)
	{
		var layout = new PlcProtocolLayout(
			ManagingDb: managingDb ?? ManagingDbLayout.Default,
			IntDb: BuildTestDataDbLayout(3),
			FloatDb: BuildTestDataDbLayout(4),
			StringDb: BuildTestDataDbLayout(5),
			ExecutionDb: ExecutionDbLayout.Default);

		return new PlcConfiguration(
			PlcConnectionSettings.Default,
			PlcProtocolSettings.Default,
			layout);
	}

	private static RecipeMetadataRegistry BuildMinimalRecipeMetadataRegistry()
	{
		var config = new AppConfiguration(
			Properties: TestRecipeMetadataRegistryFactory.DefaultStringProperty(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default,
			Ui: AppUiOptions.Default);

		return new RecipeMetadataRegistry(config);
	}

	private static (PlcTransactionExecutor executor, FakeS7Transport transport) BuildExecutor(
		ManagingDbLayout? managingDb = null)
	{
		var transport = new FakeS7Transport();
		var converter = new RecipeConverter(BuildMinimalRecipeMetadataRegistry());
		var configuration = BuildTestConfiguration(managingDb);
		var arrayCodec = TestArrayCodecFactory.Create(configuration);
		var executor = new PlcTransactionExecutor(
			transport, converter, arrayCodec, configuration, NullLogger<PlcTransactionExecutor>.Instance);

		return (executor, transport);
	}

	// Builds an 8-byte header buffer for a DB with <paramref name="currentSize"/> elements,
	// using the test layout (currentSize at offset 4).
	private static byte[] BuildArrayHeaderBytes(uint currentSize)
	{
		var header = new byte[8];
		BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0), currentSize);
		BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), currentSize);
		return header;
	}

	// Configures the transport to return empty-array headers (count=0) for all three data DBs,
	// so that PlcTransactionExecutor.ReadRecipeDataAsync succeeds and returns zero-length arrays.
	private static void ConfigureEmptyArrayReadResponses(FakeS7Transport transport)
	{
		var emptyHeader = BuildArrayHeaderBytes(0);
		var layout = BuildTestConfiguration().Layout;

		// For each DB, the header read fetches DataStartOffset (8) bytes, then the full data read
		// also requests DataStartOffset + 0 * elementSize = 8 bytes.
		transport.SetReadResponseForDb(layout.IntDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.FloatDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.StringDb.DbNumber, (_, count) => new byte[count]);
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WritesCommittedFalseFirst()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		var firstWrite = transport.WriteLog[0];
		firstWrite.DbNumber.Should().Be(layout.ManagingDb.DbNumber);
		firstWrite.StartByte.Should().Be(layout.ManagingDb.CommittedOffset,
			"the managing write must start at CommittedOffset so the firmware-owned version DWORD is preserved");
		firstWrite.Data[0].Should().Be(0x00,
			"committed=false must be written first");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WritesIntArraySecond()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		IndexOfFirstWriteToDb(transport, layout.IntDb.DbNumber).Should().BeGreaterThan(
			IndexOfFirstWriteToDb(transport, layout.ManagingDb.DbNumber),
			"int array must be written after the initial committed=false managing write");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WritesFloatArrayAfterInt()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		IndexOfFirstWriteToDb(transport, layout.FloatDb.DbNumber).Should().BeGreaterThan(
			IndexOfFirstWriteToDb(transport, layout.IntDb.DbNumber),
			"float array must be written after int array");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WritesStringArrayAfterFloat()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		IndexOfFirstWriteToDb(transport, layout.StringDb.DbNumber).Should().BeGreaterThan(
			IndexOfFirstWriteToDb(transport, layout.FloatDb.DbNumber),
			"string array must be written after float array");
	}

	private static int IndexOfFirstWriteToDb(FakeS7Transport transport, int dbNumber)
	{
		return transport.WriteLog.FindIndex(w => w.DbNumber == dbNumber);
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_CallsReadForVerification()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		transport.ReadLog.Should().Contain(
			r => r.DbNumber == layout.IntDb.DbNumber,
			"ReadRecipeDataAsync must be called after write for verification");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_CommitsArraysAndLines_AfterUncommittedWrite()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		var committedWrites = transport.WriteLog
			.Where(w => w.DbNumber == layout.ManagingDb.DbNumber
				&& w.StartByte == layout.ManagingDb.CommittedOffset)
			.ToList();

		committedWrites.Should().HaveCount(3,
			"the committed flag should be written three times: committed=false, committed=false+lines, committed=true");

		committedWrites[0].Data[0].Should().Be(0x00,
			"first committed write: committed=false");
		committedWrites[1].Data[0].Should().Be(0x00,
			"second committed write: committed=false with recipe_lines set");
		committedWrites[2].Data[0].Should().Be(0x01,
			"third committed write: committed=true");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_VerificationMismatch_RetriesUpToMaxAttempts()
	{
		var (executor, transport) = BuildExecutor();
		var layout = BuildTestConfiguration().Layout;

		// Return a mismatched int count (1 instead of 0) so verification always fails.
		var mismatchHeader = BuildArrayHeaderBytes(1);
		transport.SetReadResponse(layout.IntDb.DbNumber, 0, 8, mismatchHeader);

		// For the full read after getting count=1, must return valid-length data.
		var fullIntData = new byte[8 + 1 * 4]; // header(8) + 1 int(4)
		BinaryPrimitives.WriteUInt32BigEndian(fullIntData.AsSpan(0), 1);
		BinaryPrimitives.WriteUInt32BigEndian(fullIntData.AsSpan(4), 1);
		transport.SetReadResponse(layout.IntDb.DbNumber, 0, 8 + 4, fullIntData);

		// Float and string return empty (count=0)
		transport.SetReadResponseForDb(layout.FloatDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.StringDb.DbNumber, (_, count) => new byte[count]);

		var result = await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		result.IsFailed.Should().BeTrue(
			"after exhausting all retry attempts the result must be failed");
		result.Errors.Should().ContainSingle(e =>
			e.Message.Contains("verification failed", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_VerificationMismatch_ExactlyMaxAttemptsArePerformed()
	{
		var (executor, transport) = BuildExecutor();
		var layout = BuildTestConfiguration().Layout;

		var maxRetryAttempts = PlcProtocolSettings.Default.MaxRetryAttempts;

		// Mismatch: read-back claims 1 int element, but write was 0.
		var mismatchHeader = BuildArrayHeaderBytes(1);
		transport.SetReadResponse(layout.IntDb.DbNumber, 0, 8, mismatchHeader);

		var fullIntData = new byte[8 + 4];
		BinaryPrimitives.WriteUInt32BigEndian(fullIntData.AsSpan(0), 1);
		BinaryPrimitives.WriteUInt32BigEndian(fullIntData.AsSpan(4), 1);
		transport.SetReadResponse(layout.IntDb.DbNumber, 0, 8 + 4, fullIntData);

		transport.SetReadResponseForDb(layout.FloatDb.DbNumber, (_, count) => new byte[count]);
		transport.SetReadResponseForDb(layout.StringDb.DbNumber, (_, count) => new byte[count]);

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		var intHeaderReads = transport.ReadLog
			.Count(r => r.DbNumber == layout.IntDb.DbNumber && r.Count == 8);

		intHeaderReads.Should().Be(maxRetryAttempts,
			"int header should be read once per retry attempt during verification");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_NotConnected_ReturnsFailedResultWithNotConnectedError()
	{
		var (executor, transport) = BuildExecutor();
		transport.SetConnected(false);

		var result = await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		result.IsFailed.Should().BeTrue("writing to a disconnected PLC must return a failed result");
		result.HasError<NotConnectedError>().Should().BeTrue(
			"the failure reason must be a NotConnectedError");
	}

	[Fact]
	public async Task ReadProtocolVersionAsync_ReturnsBigEndianValueAtVersionOffset()
	{
		var (executor, transport) = BuildExecutor();
		var layout = BuildTestConfiguration().Layout;

		var versionBytes = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32BigEndian(versionBytes, 7);
		transport.SetReadResponse(
			layout.ManagingDb.DbNumber, layout.ManagingDb.VersionOffset, sizeof(int), versionBytes);

		var result = await executor.ReadProtocolVersionAsync(TestContext.Current.CancellationToken);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().Be(7, "the version is decoded big-endian from the managing DB VersionOffset");
	}

	[Fact]
	public async Task ReadProtocolVersionAsync_NotConnected_ReturnsNotConnectedError()
	{
		var (executor, transport) = BuildExecutor();
		transport.SetConnected(false);

		var result = await executor.ReadProtocolVersionAsync(TestContext.Current.CancellationToken);

		result.IsFailed.Should().BeTrue();
		result.HasError<NotConnectedError>().Should().BeTrue();
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_NeverWritesManagingAreaVersionBytes()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		AssertNoManagingWriteTouchesVersionField(transport, layout.ManagingDb);
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_ReorderedLayout_NeverWritesVersionField()
	{
		// Version is NOT at offset 0 here; the writable fields straddle it. A contiguous-slab write
		// from CommittedOffset would clobber the version field, so this proves each field is written
		// at its own offset regardless of layout ordering.
		var reorderedLayout = new ManagingDbLayout(
			DbNumber: 2,
			VersionOffset: 5,
			CommittedOffset: 0,
			RecipeLinesOffset: 1,
			TotalSize: 9);
		var (executor, transport) = BuildExecutor(reorderedLayout);
		ConfigureEmptyArrayReadResponses(transport);

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty, TestContext.Current.CancellationToken);

		AssertNoManagingWriteTouchesVersionField(transport, reorderedLayout);
	}

	private static void AssertNoManagingWriteTouchesVersionField(
		FakeS7Transport transport, ManagingDbLayout managingDb)
	{
		var managingWrites = transport.WriteLog
			.Where(w => w.DbNumber == managingDb.DbNumber)
			.ToList();

		managingWrites.Should().NotBeEmpty();

		var versionStart = managingDb.VersionOffset;
		var versionEnd = managingDb.VersionOffset + sizeof(int);

		managingWrites.Should().OnlyContain(
			w => w.StartByte + w.Data.Length <= versionStart || w.StartByte >= versionEnd,
			"no managing write may overlap the firmware-owned version field, regardless of field ordering");
	}
}
