using System.Buffers.Binary;

using Microsoft.Extensions.Time.Testing;

using SemiStep.Tools.PlcEmulator;
using SemiStep.Tools.PlcEmulator.Ui;

namespace SemiStep.Tests.Emulator;

internal sealed class EmulatorWindowHarness : IDisposable
{
	private const int TickLimit = 100;

	private EmulatorWindowHarness(RunEngineHarness engine)
	{
		Engine = engine;
		ViewModel = new MainWindowViewModel(engine.Plc, engine.Engine, engine.Loop, "MBE", engine.Time);
	}

	public RunEngineHarness Engine { get; }

	public MainWindowViewModel ViewModel { get; }

	public FakeTimeProvider Time => Engine.Time;

	public EmulatedPlc Plc => Engine.Plc;

	public static async Task<EmulatorWindowHarness> CreateAsync()
	{
		return new EmulatorWindowHarness(await RunEngineHarness.CreateAsync());
	}

	public DataBlockViewModel Block(int dbNumber)
	{
		return ViewModel.Blocks.Single(block => block.Schema.Info.DbNumber == dbNumber);
	}

	public MemoryRowViewModel Row(string name)
	{
		return ViewModel.SelectedBlock.Rows.Single(row => row.Name == name);
	}

	public IEnumerable<string> RowNames()
	{
		return ViewModel.SelectedBlock.Rows.Select(row => row.Name);
	}

	public int ReadInt(int dbNumber, int offset)
	{
		return BinaryPrimitives.ReadInt32BigEndian(Plc.Read(dbNumber, offset, sizeof(int)));
	}

	public void WriteInt(int dbNumber, int offset, int value)
	{
		var bytes = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32BigEndian(bytes, value);
		Plc.Write(dbNumber, offset, bytes);
	}

	public void Refresh(int count = 1)
	{
		for (var i = 0; i < count; i++)
		{
			Time.Advance(RunEngine.TickInterval);
		}
	}

	public void TickUntil(Func<RunPhase, bool> reached)
	{
		for (var i = 0; i < TickLimit && !reached(Engine.Loop.State.Phase); i++)
		{
			Engine.Loop.Tick();
		}

		Refresh();
	}

	public void Dispose()
	{
		ViewModel.Dispose();
		Engine.Dispose();
	}
}
