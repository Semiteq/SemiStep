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

	private static PlcConfiguration BuildTestConfiguration()
	{
		var layout = new PlcProtocolLayout(
			ManagingDb: ManagingDbLayout.Default,
			IntDb: BuildTestDataDbLayout(3),
			FloatDb: BuildTestDataDbLayout(4),
			StringDb: BuildTestDataDbLayout(5),
			ExecutionDb: ExecutionDbLayout.Default);

		return new PlcConfiguration(
			PlcConnectionSettings.Default,
			PlcProtocolSettings.Default,
			layout);
	}

	private static RecipeMetadataRegistry BuildEmptyRecipeMetadataRegistry()
	{
		var config = new AppConfiguration(
			Properties: new Dictionary<string, PropertyTypeDefinition>(),
			Columns: new Dictionary<string, GridColumnDefinition>(),
			Groups: new Dictionary<string, GroupDefinition>(),
			Actions: new Dictionary<int, ActionDefinition>(),
			GridStyle: GridStyleOptions.Default,
			PlcConfiguration: PlcConfiguration.Default);

		return new RecipeMetadataRegistry(config);
	}

	private static (PlcTransactionExecutor executor, FakeS7Transport transport) BuildExecutor()
	{
		var transport = new FakeS7Transport();
		var converter = new RecipeConverter(BuildEmptyRecipeMetadataRegistry());
		var configuration = BuildTestConfiguration();
		var executor = new PlcTransactionExecutor(
			transport, converter, configuration, NullLogger<PlcTransactionExecutor>.Instance);

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

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

		var firstWrite = transport.WriteLog[0];
		firstWrite.DbNumber.Should().Be(layout.ManagingDb.DbNumber);
		firstWrite.Data[layout.ManagingDb.CommittedOffset].Should().Be(0x00,
			"committed=false must be written first");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WritesIntArraySecond()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

		var secondWrite = transport.WriteLog[1];
		secondWrite.DbNumber.Should().Be(layout.IntDb.DbNumber,
			"int array must be written after committed=false");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WritesFloatArrayThird()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

		var thirdWrite = transport.WriteLog[2];
		thirdWrite.DbNumber.Should().Be(layout.FloatDb.DbNumber,
			"float array must be written after int array");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WritesStringArrayFourth()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

		var fourthWrite = transport.WriteLog[3];
		fourthWrite.DbNumber.Should().Be(layout.StringDb.DbNumber,
			"string array must be written after float array");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WritesCommittedTrueLast()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

		// Last managing-area write is committed=true
		var lastManagingWrite = transport.WriteLog
			.Where(w => w.DbNumber == layout.ManagingDb.DbNumber)
			.Last();

		lastManagingWrite.Data[layout.ManagingDb.CommittedOffset].Should().Be(0x01,
			"committed=true must be the last write to the managing area");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_CallsReadForVerification()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

		transport.ReadLog.Should().Contain(
			r => r.DbNumber == layout.IntDb.DbNumber,
			"ReadRecipeDataAsync must be called after write for verification");
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_EmptyRecipe_WriteSequenceIsCommittedFalse_Arrays_RecipeLines_CommittedTrue()
	{
		var (executor, transport) = BuildExecutor();
		ConfigureEmptyArrayReadResponses(transport);
		var layout = BuildTestConfiguration().Layout;

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

		var managingWrites = transport.WriteLog
			.Where(w => w.DbNumber == layout.ManagingDb.DbNumber)
			.ToList();

		managingWrites.Should().HaveCount(3,
			"managing area should be written: committed=false, committed=false+lines, committed=true");

		managingWrites[0].Data[layout.ManagingDb.CommittedOffset].Should().Be(0x00,
			"first managing write: committed=false");
		managingWrites[1].Data[layout.ManagingDb.CommittedOffset].Should().Be(0x00,
			"second managing write: committed=false with recipe_lines set");
		managingWrites[2].Data[layout.ManagingDb.CommittedOffset].Should().Be(0x01,
			"third managing write: committed=true");
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

		var result = await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

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

		await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

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

		var result = await executor.WriteRecipeWithRetryAsync(Recipe.Empty);

		result.IsFailed.Should().BeTrue("writing to a disconnected PLC must return a failed result");
		result.HasError<NotConnectedError>().Should().BeTrue(
			"the failure reason must be a NotConnectedError");
	}
}
