using FluentResults;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Core.Recipes.Analysis.Warnings;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>Takes one consistent copy of the committed recipe from the emulated memory and decodes it like the app does.</summary>
internal sealed class CommittedRecipeLoader
{
	private readonly EmulatedPlc _plc;
	private readonly RecipeMetadataRegistry _registry;
	private readonly ManagingAreaCodec _managingCodec;
	private readonly ArrayCodec _arrayCodec;
	private readonly RecipeConverter _recipeConverter;
	private readonly RecipeAnalyzer _recipeAnalyzer;

	public CommittedRecipeLoader(EmulatedPlc plc, AppConfiguration configuration)
	{
		_plc = plc;
		_registry = new RecipeMetadataRegistry(configuration);
		var layout = plc.Layout;
		_managingCodec = new ManagingAreaCodec(layout.ManagingDb);
		_arrayCodec = new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, _registry.GetStringMaxLength());
		_recipeConverter = new RecipeConverter(_registry);
		_recipeAnalyzer = new RecipeAnalyzer(_registry);
	}

	public Result<RunPlan> Load()
	{
		var layout = _plc.Layout;
		var blocks = _plc.ReadBlocks(
			layout.ManagingDb.DbNumber,
			layout.IntDb.DbNumber,
			layout.FloatDb.DbNumber,
			layout.StringDb.DbNumber);

		var managing = _managingCodec.Decode(blocks[layout.ManagingDb.DbNumber]).Value;
		if (!managing.Committed)
		{
			return Result.Fail(new RunRefusal(RunRefusalReason.NoCommittedRecipe, "no committed recipe"));
		}

		var dataResult = DecodeRecipeData(blocks, managing.RecipeLines);
		if (dataResult.IsFailed)
		{
			return Result.Fail(DoesNotDecode(dataResult));
		}

		var recipeResult = _recipeConverter.ToRecipe(dataResult.Value);
		return recipeResult.IsFailed
			? Result.Fail(DoesNotDecode(recipeResult))
			: Analyze(recipeResult.Value);
	}

	private Result<RunPlan> Analyze(Recipe recipe)
	{
		if (recipe.StepCount == 0)
		{
			return Result.Fail(new RunRefusal(RunRefusalReason.RecipeHasNoLines, "committed recipe has no lines"));
		}

		var analysis = _recipeAnalyzer.Analyze(recipe);
		if (analysis.IsFailed)
		{
			return Result.Fail(FailsAnalysis(Describe(analysis)));
		}

		var loopFault = analysis.Reasons
			.FirstOrDefault(reason => reason is UnmatchedEndForWarning or UnclosedForLoopWarning);
		if (loopFault is not null)
		{
			return Result.Fail(FailsAnalysis(loopFault.Message));
		}

		var emptyLoop = analysis.Value.Loops.FirstOrDefault(loop => loop.Iterations <= 0);
		if (emptyLoop is not null)
		{
			return Result.Fail(
				FailsAnalysis($"For at line {emptyLoop.StartIndex + 1} has count {emptyLoop.Iterations}"));
		}

		return Result.Ok(new RunPlan(analysis.Value, _registry));
	}

	private Result<PlcRecipeData> DecodeRecipeData(IReadOnlyDictionary<int, byte[]> blocks, int stepCount)
	{
		var layout = _plc.Layout;
		var intBlock = blocks[layout.IntDb.DbNumber];
		var floatBlock = blocks[layout.FloatDb.DbNumber];
		var stringBlock = blocks[layout.StringDb.DbNumber];
		var intCount = CountOf(intBlock, layout.IntDb, ProtocolConstants.IntElementSize, "int");
		var floatCount = CountOf(floatBlock, layout.FloatDb, ProtocolConstants.FloatElementSize, "float");
		var stringCount = CountOf(stringBlock, layout.StringDb, _arrayCodec.WStringElementSize, "string");
		var counts = Result.Merge(intCount, floatCount, stringCount);
		if (counts.IsFailed)
		{
			return Result.Fail(counts.Errors);
		}

		return Result.Ok(new PlcRecipeData(
			_arrayCodec.DecodeIntArray(intBlock, intCount.Value),
			_arrayCodec.DecodeFloatArray(floatBlock, floatCount.Value),
			_arrayCodec.DecodeStringArray(stringBlock, stringCount.Value),
			stepCount));
	}

	private static Result<int> CountOf(byte[] block, DataDbLayout layout, int elementSize, string name)
	{
		var count = ArrayCodec.ReadArrayCurrentSize(block, layout);
		var capacity = (block.Length - layout.DataStartOffset) / elementSize;
		return count < 0 || count > capacity
			? Result.Fail($"{name} array holds {count} elements, the block holds {capacity}")
			: Result.Ok(count);
	}

	private static RunRefusal DoesNotDecode(IResultBase result)
	{
		return new RunRefusal(RunRefusalReason.RecipeDoesNotDecode, $"recipe does not decode: {Describe(result)}");
	}

	private static RunRefusal FailsAnalysis(string reason)
	{
		return new RunRefusal(RunRefusalReason.RecipeFailsAnalysis, $"recipe does not pass analysis: {reason}");
	}

	private static string Describe(IResultBase result)
	{
		return string.Join("; ", result.Errors.Select(error => error.Message));
	}
}
