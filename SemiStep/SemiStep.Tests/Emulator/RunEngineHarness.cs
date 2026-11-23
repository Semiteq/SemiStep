using System.Buffers.Binary;
using System.Collections.Concurrent;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.S7.Serialization;
using SemiStep.Core.Plc.State;
using SemiStep.Core.Recipes;
using SemiStep.Core.Recipes.Analysis;
using SemiStep.Tests.Helpers;
using SemiStep.Tools.PlcEmulator;

namespace SemiStep.Tests.Emulator;

/// <summary>An emulator on the MBE preset (its For action carries a count) with the run engine on a fake clock.</summary>
internal sealed class RunEngineHarness : IDisposable
{
	private readonly RecipeConverter _recipeConverter;
	private readonly ArrayCodec _arrayCodec;
	private readonly ExecutionStateCodec _executionCodec;

	private RunEngineHarness(AppConfiguration configuration, EmulatedPlc plc)
	{
		Plc = plc;
		Configuration = configuration;
		var registry = new RecipeMetadataRegistry(configuration);
		var layout = plc.Layout;
		_recipeConverter = new RecipeConverter(registry);
		_arrayCodec = new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, registry.GetStringMaxLength());
		_executionCodec = new ExecutionStateCodec(layout.ExecutionDb);
		Engine = new RunEngine(plc, new CommittedRecipeLoader(plc, configuration));
		Loop = new EmulatorLoop(plc, Engine);
		Loop.EventRaised += Events.Enqueue;
	}

	public FakeTimeProvider Time { get; } = new();

	public AppConfiguration Configuration { get; }

	public EmulatedPlc Plc { get; }

	public ConcurrentQueue<EmulatorEvent> Events { get; } = new();

	public RunEngine Engine { get; }

	public EmulatorLoop Loop { get; }

	public static async Task<RunEngineHarness> CreateAsync()
	{
		var configuration = await ShippedConfigLocator.LoadAsync("MBE");
		return new RunEngineHarness(configuration, EmulatorTestSupport.StartOrFail(configuration, EmulatorTestSupport.FreePort()));
	}

	public static Step Wait(float seconds)
	{
		return new Step((int)ServiceActionId.Wait, []).WithProperty("step_duration", PropertyValue.FromFloat(seconds));
	}

	public static Step For(float count)
	{
		return new Step((int)ServiceActionId.ForLoop, []).WithProperty("task", PropertyValue.FromFloat(count));
	}

	public static Step EndFor()
	{
		return new Step((int)ServiceActionId.EndForLoop, []);
	}

	public static Step Pause()
	{
		return new Step((int)ServiceActionId.Pause, []);
	}

	public static Recipe RecipeOf(params Step[] steps)
	{
		return steps.Aggregate(Recipe.Empty, (recipe, step) => recipe.AppendStep(step));
	}

	public void Commit(Recipe recipe, int? declaredLines = null)
	{
		var data = _recipeConverter.FromRecipe(recipe).Value;
		var layout = Plc.Layout;
		Plc.Write(layout.IntDb.DbNumber, 0, _arrayCodec.EncodeIntArray(data.IntValues));
		Plc.Write(layout.FloatDb.DbNumber, 0, _arrayCodec.EncodeFloatArray(data.FloatValues));
		Plc.Write(layout.StringDb.DbNumber, 0, _arrayCodec.EncodeStringArray(data.StringValues));

		var lines = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32BigEndian(lines, declaredLines ?? recipe.StepCount);
		Plc.Write(layout.ManagingDb.DbNumber, layout.ManagingDb.RecipeLinesOffset, lines);
		Plc.Write(layout.ManagingDb.DbNumber, layout.ManagingDb.CommittedOffset, [1]);
	}

	public PlcExecutionInfo ReadExecution()
	{
		var execution = Plc.Layout.ExecutionDb;
		var decoded = _executionCodec.Decode(Plc.Read(execution.DbNumber, 0, execution.TotalSize));
		decoded.IsSuccess.Should().BeTrue();
		return decoded.Value;
	}

	public PlcExecutionInfo Tick(int count = 1)
	{
		for (var i = 0; i < count; i++)
		{
			Engine.Tick();
		}

		return ReadExecution();
	}

	public void Dispose()
	{
		Plc.Dispose();
	}
}
