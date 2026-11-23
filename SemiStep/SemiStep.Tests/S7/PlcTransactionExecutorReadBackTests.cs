using System.Buffers.Binary;

using AwesomeAssertions;

using FluentResults;

using Microsoft.Extensions.Logging.Abstractions;

using SemiStep.Core.Configuration.Facade;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.Sync;
using SemiStep.Core.Recipes;
using SemiStep.Tests.Helpers;
using SemiStep.Tests.S7.Helpers;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "ReadBack")]
[Trait("Category", "Integration")]
public sealed class PlcTransactionExecutorReadBackTests
{
	[Fact]
	public async Task ReadRecipeFromPlcAsync_StepsWithExtraIntColumns_ReadsBackEqualRecipe()
	{
		var fixture = await BuildFixtureAsync();
		var recipe = RieRecipeBuilder.BuildThreeStepRecipe().AppendStep(RieRecipeBuilder.BuildPumpDownStep());
		await WriteAndAssertSuccessAsync(fixture, recipe);

		var result = await fixture.Executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().Be(recipe);
	}

	[Fact]
	public async Task ReadRecipeFromPlcAsync_IdOnlyIntSteps_ReadsBackEqualRecipe()
	{
		var fixture = await BuildFixtureAsync();
		var recipe = Recipe.Empty
			.AppendStep(RieRecipeBuilder.BuildPumpDownStep())
			.AppendStep(RieRecipeBuilder.BuildPumpDownStep());
		await WriteAndAssertSuccessAsync(fixture, recipe);

		var result = await fixture.Executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().Be(recipe);
	}

	[Fact]
	public async Task ReadRecipeFromPlcAsync_EmptyRecipe_ReadsBackEmptyRecipe()
	{
		var fixture = await BuildFixtureAsync();
		await WriteAndAssertSuccessAsync(fixture, Recipe.Empty);

		var result = await fixture.Executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);

		result.IsSuccess.Should().BeTrue();
		result.Value.StepCount.Should().Be(0);
	}

	[Fact]
	public async Task WriteRecipeWithRetryAsync_CorrectWrite_PassesVerification()
	{
		var fixture = await BuildFixtureAsync();

		var result = await fixture.Executor.WriteRecipeWithRetryAsync(
			RieRecipeBuilder.BuildThreeStepRecipe(), TestContext.Current.CancellationToken);

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public async Task ReadRecipeFromPlcAsync_NegativeRecipeLines_FailsWithNegativeStepCountError()
	{
		var fixture = await BuildFixtureAsync();
		await WriteAndAssertSuccessAsync(fixture, RieRecipeBuilder.BuildThreeStepRecipe());
		await OverwriteRecipeLinesAsync(fixture, -1);

		var result = await fixture.Executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);

		AssertLengthFault(result, RecipeLengthFault.NegativeStepCount, headerStepCount: -1, arrayType: null);
	}

	[Fact]
	public async Task ReadRecipeFromPlcAsync_RecipeLinesAboveStepCount_FailsWithExhaustedArrayError()
	{
		var fixture = await BuildFixtureAsync();
		await WriteAndAssertSuccessAsync(fixture, RieRecipeBuilder.BuildThreeStepRecipe());
		await OverwriteRecipeLinesAsync(fixture, 4);

		var result = await fixture.Executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);

		AssertLengthFault(result, RecipeLengthFault.ArrayExhausted, headerStepCount: 4, PropertyType.Int);
	}

	[Fact]
	public async Task ReadRecipeFromPlcAsync_RecipeLinesAtIntMaxValue_FailsWithExhaustedArrayError()
	{
		var fixture = await BuildFixtureAsync();
		await WriteAndAssertSuccessAsync(fixture, RieRecipeBuilder.BuildThreeStepRecipe());
		await OverwriteRecipeLinesAsync(fixture, int.MaxValue);

		var result = await fixture.Executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);

		AssertLengthFault(result, RecipeLengthFault.ArrayExhausted, headerStepCount: int.MaxValue, PropertyType.Int);
	}

	[Fact]
	public async Task ReadRecipeFromPlcAsync_RecipeLinesBelowStepCount_FailsWithLeftoversError()
	{
		var fixture = await BuildFixtureAsync();
		await WriteAndAssertSuccessAsync(fixture, RieRecipeBuilder.BuildThreeStepRecipe());
		await OverwriteRecipeLinesAsync(fixture, 2);

		var result = await fixture.Executor.ReadRecipeFromPlcAsync(TestContext.Current.CancellationToken);

		AssertLengthFault(result, RecipeLengthFault.ArrayHasLeftovers, headerStepCount: 2, PropertyType.Int);
	}

	private static void AssertLengthFault(
		Result<Recipe> result, RecipeLengthFault fault, int headerStepCount, PropertyType? arrayType)
	{
		result.IsFailed.Should().BeTrue();
		var error = result.Errors.OfType<RecipeLengthMismatchError>().Should().ContainSingle().Subject;
		error.Fault.Should().Be(fault);
		error.HeaderStepCount.Should().Be(headerStepCount);
		error.ArrayType.Should().Be(arrayType);
	}

	private static async Task WriteAndAssertSuccessAsync(Fixture fixture, Recipe recipe)
	{
		var writeResult = await fixture.Executor.WriteRecipeWithRetryAsync(
			recipe, TestContext.Current.CancellationToken);

		writeResult.IsSuccess.Should().BeTrue();
	}

	private static async Task OverwriteRecipeLinesAsync(Fixture fixture, int recipeLines)
	{
		var managingDb = fixture.Layout.ManagingDb;
		var bytes = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32BigEndian(bytes, recipeLines);

		await fixture.Transport.WriteBytesAsync(
			managingDb.DbNumber,
			managingDb.RecipeLinesOffset,
			bytes,
			TestContext.Current.CancellationToken);
	}

	private static async Task<Fixture> BuildFixtureAsync()
	{
		var path = ShippedConfigLocator.GetConfigDirectory("RIE");
		var loaded = await ConfigFacade.LoadAndValidateAsync(path);
		loaded.IsSuccess.Should().BeTrue();

		var configuration = loaded.Value.PlcConfiguration;
		var registry = new RecipeMetadataRegistry(loaded.Value);
		var layout = configuration.Layout;
		var arrayCodec = new ArrayCodec(
			layout.IntDb, layout.FloatDb, layout.StringDb, registry.GetStringMaxLength());
		var transport = new StoringS7Transport();
		var executor = new PlcTransactionExecutor(
			transport,
			new RecipeConverter(registry),
			arrayCodec,
			configuration,
			NullLogger<PlcTransactionExecutor>.Instance);

		return new Fixture(executor, transport, layout);
	}

	private sealed record Fixture(
		PlcTransactionExecutor Executor,
		StoringS7Transport Transport,
		PlcProtocolLayout Layout);
}
