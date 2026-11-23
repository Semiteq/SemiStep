using System.Globalization;
using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using AwesomeAssertions;

using ReactiveUI;

using S7.Net;

using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Tests.Helpers;
using SemiStep.Tools.PlcEmulator;
using SemiStep.Tools.PlcEmulator.Ui;

using Xunit;

using static SemiStep.Tests.Emulator.RunEngineHarness;

namespace SemiStep.Tests.Emulator;

[Trait("Component", "UI")]
[Trait("Area", "Emulator")]
[Trait("Category", "Integration")]
public sealed class EmulatorWindowTests
{
	private static readonly TimeSpan _flashDuration = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(10);

	[AvaloniaFact]
	public async Task Opens_Idle_WithTheConfiguredBlocksAndTheExecutionBlockSelected()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var viewModel = window.ViewModel;

		viewModel.Title.Should().Contain("MBE").And.Contain(window.Plc.Endpoint);
		viewModel.Blocks.Select(block => block.Schema.Info.DbNumber)
			.Should().Equal(window.Plc.Blocks.Select(block => block.DbNumber));
		viewModel.SelectedBlock.Schema.Info.DbNumber.Should().Be(window.Plc.Layout.ExecutionDb.DbNumber);
		window.RowNames().Should().Equal(
			"recipe_active",
			"actual_line",
			"step_current_time",
			"for_loop_count1",
			"for_loop_count2",
			"for_loop_count3");
		viewModel.State.Phase.Should().Be(RunPhase.Idle);
		viewModel.IsClientConnected.Should().BeFalse();
		(await ButtonStates(viewModel)).Should().Equal(true, false, false);
		window.Row("actual_line").Offset.Should().Be($"{window.Plc.Layout.ExecutionDb.ActualLineOffset}.0");
	}

	[AvaloniaFact]
	public async Task ManagingBlock_ShowsTheProtocolVersionSeed()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();

		window.ViewModel.SelectedBlock = window.Block(window.Plc.Layout.ManagingDb.DbNumber);

		window.Row("version").Value.Should().Be(Text(ProtocolConstants.ProtocolVersion));
		window.Row("committed").Value.Should().Be("FALSE");
	}

	[AvaloniaFact]
	public async Task ArrayRows_FollowCurrentSize()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		window.ViewModel.SelectedBlock = window.Block(window.Plc.Layout.IntDb.DbNumber);
		window.RowNames().Should().Equal("capacity", "current_size");

		WriteIntCurrentSize(window, 3);
		window.Refresh();
		window.RowNames().Should().Equal("capacity", "current_size", "data[0]", "data[1]", "data[2]");

		WriteIntCurrentSize(window, 1);
		window.Refresh();
		window.RowNames().Should().Equal("capacity", "current_size", "data[0]");
	}

	[AvaloniaTheory]
	[InlineData(-5)]
	[InlineData(1_000_000)]
	public async Task ArrayRows_CurrentSizeOutsideTheBuffer_StopAtTheAllocatedCapacityAndReportOnce(int currentSize)
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		window.ViewModel.SelectedBlock = window.Block(window.Plc.Layout.IntDb.DbNumber);

		WriteIntCurrentSize(window, currentSize);
		window.Refresh();

		var allocated = window.ViewModel.SelectedBlock.Schema.AllocatedCapacity;
		var expectedRows = 2 + Math.Clamp(currentSize, 0, allocated);
		window.ViewModel.SelectedBlock.Rows.Should().HaveCount(expectedRows);
		window.ViewModel.Message.IsError.Should().BeTrue();

		window.ViewModel.Edit(window.Row("capacity"), "not a number");
		var afterEdit = window.ViewModel.Message;
		window.Refresh(3);
		window.ViewModel.Message.Should().Be(afterEdit);
	}

	[AvaloniaTheory]
	[InlineData("-5")]
	[InlineData("1000000")]
	public async Task Edit_CurrentSizeOutsideTheBuffer_KeepsTheRedMessage(string text)
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		window.ViewModel.SelectedBlock = window.Block(window.Plc.Layout.IntDb.DbNumber);

		window.ViewModel.Edit(window.Row("current_size"), text);
		var afterEdit = window.ViewModel.Message;
		window.Refresh(3);

		afterEdit.IsError.Should().BeTrue();
		window.ViewModel.Message.Should().Be(afterEdit);
	}

	[AvaloniaFact]
	public async Task Refresh_FollowsMemoryChangedByTheRun_AndTheChangedRowFlashesForOneSecond()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		window.Engine.Commit(RecipeOf(Wait(0.5f), Wait(0.5f)));
		await window.ViewModel.Start.Execute();

		window.TickUntil(phase => phase == RunPhase.Running);

		window.Row("recipe_active").Value.Should().Be("TRUE");
		window.Row("recipe_active").IsChanged.Should().BeTrue();
		window.Row("for_loop_count1").IsChanged.Should().BeFalse();

		window.Refresh((int)(_flashDuration / RunEngine.TickInterval));
		window.Row("recipe_active").IsChanged.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task Refresh_CellInEditMode_IsNotOverwrittenUntilEditEnds()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var execution = window.Plc.Layout.ExecutionDb;
		var row = window.Row("actual_line");
		var initial = row.Value;
		row.IsEditing = true;

		window.WriteInt(execution.DbNumber, execution.ActualLineOffset, 42);
		window.Refresh();
		var whileEditing = row.Value;
		row.IsEditing = false;
		window.Refresh();

		whileEditing.Should().Be(initial);
		row.Value.Should().Be("42");
	}

	[AvaloniaFact]
	public async Task Edit_ValidValueWhileIdle_IsWrittenToThePlcMemory()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var intDb = window.Plc.Layout.IntDb;
		window.ViewModel.SelectedBlock = window.Block(intDb.DbNumber);

		window.ViewModel.Edit(window.Row("current_size"), "5");

		window.ReadInt(intDb.DbNumber, intDb.CurrentSizeOffset).Should().Be(5);
		window.ViewModel.Message.IsError.Should().BeFalse();
		window.Row("current_size").Value.Should().Be("5");
		window.ViewModel.SelectedBlock.Rows.Should().HaveCount(2 + 5);
	}

	[AvaloniaFact]
	public async Task Edit_VersionAndCapacity_AreWrittenLikeAnyOtherField()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var layout = window.Plc.Layout;
		window.ViewModel.SelectedBlock = window.Block(layout.ManagingDb.DbNumber);
		window.ViewModel.Edit(window.Row("version"), "7");
		window.ReadInt(layout.ManagingDb.DbNumber, layout.ManagingDb.VersionOffset).Should().Be(7);

		window.ViewModel.SelectedBlock = window.Block(layout.IntDb.DbNumber);
		window.ViewModel.Edit(window.Row("capacity"), "9");

		window.ReadInt(layout.IntDb.DbNumber, layout.IntDb.CapacityOffset).Should().Be(9);
		window.ViewModel.Message.IsError.Should().BeFalse();
	}

	[AvaloniaFact]
	public async Task Edit_FloatAndStringElements_RoundTripThroughTheMemory()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var layout = window.Plc.Layout;
		window.ViewModel.SelectedBlock = window.Block(layout.FloatDb.DbNumber);
		window.ViewModel.Edit(window.Row("current_size"), "2");
		window.ViewModel.Edit(window.Row("data[1]"), "12,5");
		window.Row("data[1]").Value.Should().Be("12.5");

		window.ViewModel.SelectedBlock = window.Block(layout.StringDb.DbNumber);
		window.ViewModel.Edit(window.Row("current_size"), "1");
		window.ViewModel.Edit(window.Row("data[0]"), "'abc'");

		window.Row("data[0]").Value.Should().Be("'abc'");
		window.ViewModel.Message.IsError.Should().BeFalse();
	}

	[AvaloniaTheory]
	[InlineData("actual_line", "abc")]
	[InlineData("actual_line", "99999999999")]
	[InlineData("step_current_time", "x")]
	[InlineData("step_current_time", "1e99")]
	[InlineData("recipe_active", "maybe")]
	public async Task Edit_InvalidValue_WritesNothingAndShowsAnError(string rowName, string text)
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var execution = window.Plc.Layout.ExecutionDb;
		var before = window.Plc.Read(execution.DbNumber, 0, execution.TotalSize);

		window.ViewModel.Edit(window.Row(rowName), text);

		window.Plc.Read(execution.DbNumber, 0, execution.TotalSize).Should().Equal(before);
		window.ViewModel.Message.IsError.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Edit_StringLongerThanTheConfiguredLength_WritesNothingAndShowsAnError()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var stringDb = window.Plc.Layout.StringDb;
		window.ViewModel.SelectedBlock = window.Block(stringDb.DbNumber);
		window.ViewModel.Edit(window.Row("current_size"), "1");
		var size = window.ViewModel.SelectedBlock.Schema.Info.Size;
		var before = window.Plc.Read(stringDb.DbNumber, 0, size);

		window.ViewModel.Edit(window.Row("data[0]"), new string('x', window.Plc.StringMaxChars + 1));

		window.Plc.Read(stringDb.DbNumber, 0, size).Should().Equal(before);
		window.ViewModel.Message.IsError.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Edit_WhileARunIsActive_WritesNothing()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		window.Engine.Commit(RecipeOf(Wait(1f)));
		await window.ViewModel.Start.Execute();
		var intDb = window.Plc.Layout.IntDb;
		window.ViewModel.SelectedBlock = window.Block(intDb.DbNumber);
		var before = window.Plc.Read(intDb.DbNumber, 0, 8);

		window.ViewModel.Edit(window.Row("capacity"), "7");

		window.ViewModel.State.IsActive.Should().BeTrue();
		window.Plc.Read(intDb.DbNumber, 0, 8).Should().Equal(before);
		window.ViewModel.Message.IsError.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Start_NothingCommitted_RefusesAndStaysIdle()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();

		await window.ViewModel.Start.Execute();

		window.ViewModel.State.Phase.Should().Be(RunPhase.Idle);
		window.ViewModel.Message.IsError.Should().BeTrue();
		(await ButtonStates(window.ViewModel)).Should().Equal(true, false, false);
	}

	[AvaloniaFact]
	public async Task Start_RecipeWithABrokenStringHeader_RefusesInRedAndStaysIdle()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		window.Engine.Commit(RecipeOf(Wait(1f)));
		var stringDb = window.Plc.Layout.StringDb;
		window.WriteInt(stringDb.DbNumber, stringDb.CurrentSizeOffset, 1);
		window.Plc.Write(stringDb.DbNumber, stringDb.DataStartOffset + 2, [0xFF, 0xFF]);

		((ICommand)window.ViewModel.Start).Execute(null);
		Dispatcher.UIThread.RunJobs();

		window.ViewModel.State.Phase.Should().Be(RunPhase.Idle);
		window.ViewModel.Message.IsError.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task RunControl_FollowsThePhaseThroughPauseStopAndTheEnd()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var viewModel = window.ViewModel;
		window.Engine.Commit(RecipeOf(Wait(0.1f), Pause(), Wait(0.1f)));

		await viewModel.Start.Execute();
		viewModel.State.Phase.Should().Be(RunPhase.Starting);
		viewModel.State.Should().Be(window.Engine.Loop.State);
		viewModel.State.IsActive.Should().BeTrue();
		(await ButtonStates(viewModel)).Should().Equal(false, false, false);

		window.TickUntil(phase => phase == RunPhase.Paused);
		viewModel.State.Should().Be(window.Engine.Loop.State);
		(await ButtonStates(viewModel)).Should().Equal(false, true, true);

		await viewModel.Continue.Execute();
		window.TickUntil(phase => phase == RunPhase.Finishing);
		viewModel.State.Should().Be(window.Engine.Loop.State);
		viewModel.State.DisplayLine.Should().Be(3);
		window.Row("actual_line").Value.Should().Be("3");

		var beforeIdle = viewModel.Message;
		window.TickUntil(phase => phase == RunPhase.Idle);
		viewModel.State.Phase.Should().Be(RunPhase.Idle);
		viewModel.State.IsActive.Should().BeFalse();
		viewModel.Message.IsError.Should().BeFalse();
		viewModel.Message.Should().NotBe(beforeIdle);
		(await ButtonStates(viewModel)).Should().Equal(true, false, false);
	}

	[AvaloniaFact]
	public async Task Stop_DuringARun_FinishesAtTheStoppedLineThenReturnsToIdle()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var viewModel = window.ViewModel;
		window.Engine.Commit(RecipeOf(Wait(5f), Wait(5f)));
		await viewModel.Start.Execute();
		window.TickUntil(phase => phase == RunPhase.Running);

		await viewModel.Stop.Execute();
		window.Refresh();

		viewModel.State.Phase.Should().Be(RunPhase.Finishing);
		viewModel.State.Should().Be(window.Engine.Loop.State);
		var afterStop = viewModel.Message;
		window.TickUntil(phase => phase == RunPhase.Idle);
		viewModel.State.Phase.Should().Be(RunPhase.Idle);
		viewModel.Message.Should().Be(afterStop);
	}

	[AvaloniaFact]
	public async Task Stop_DuringAPause_FinishesAtThePausedLineThenReturnsToIdle()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		var viewModel = window.ViewModel;
		window.Engine.Commit(RecipeOf(Wait(0.1f), Pause(), Wait(0.1f)));
		await viewModel.Start.Execute();
		window.TickUntil(phase => phase == RunPhase.Paused);
		var pausedLine = viewModel.State.DisplayLine;

		await viewModel.Stop.Execute();
		window.Refresh();

		viewModel.State.Phase.Should().Be(RunPhase.Finishing);
		viewModel.State.DisplayLine.Should().Be(pausedLine);
		window.TickUntil(phase => phase == RunPhase.Idle);
		viewModel.State.Phase.Should().Be(RunPhase.Idle);
	}

	[AvaloniaFact]
	public async Task Tick_Throws_TheLoopEndsAndTheMessageLineShowsItInRed()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		window.Engine.Loop.EventRaised += emulatorEvent =>
		{
			if (emulatorEvent is RecipeCommitted)
			{
				throw new InvalidOperationException("tick fault");
			}
		};
		using var cancellation = new CancellationTokenSource();
		var looping = window.Engine.Loop.RunAsync(window.Time, cancellation.Token);
		window.Engine.Commit(RecipeOf(Wait(1f)));

		window.Time.Advance(RunEngine.TickInterval);
		await looping.WaitAsync(_waitLimit, TestContext.Current.CancellationToken);
		Dispatcher.UIThread.RunJobs();

		window.ViewModel.Message.IsError.Should().BeTrue();
	}

	[AvaloniaFact]
	public async Task Events_RecipeCommittedFromTheTickThread_ShowOnTheMessageLine()
	{
		using var window = await EmulatorWindowHarness.CreateAsync();
		window.Engine.Commit(RecipeOf(Wait(1f), Wait(1f), Wait(1f)));

		await Task.Run(window.Engine.Loop.Tick, TestContext.Current.CancellationToken);
		Dispatcher.UIThread.RunJobs();

		window.ViewModel.Message.IsError.Should().BeFalse();
		window.ViewModel.Message.Text.Should().Contain("3");
	}

	[AvaloniaFact]
	public async Task Events_ClientConnectsAndDisconnects_TheIndicatorFollows()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		using var window = await EmulatorWindowHarness.CreateAsync();
		using var client = new Plc(CpuType.S71500, "127.0.0.1", window.Plc.Port, 0, 1);

		await client.OpenAsync(cancellationToken);
		await WaitUntilTickedAsync(window, viewModel => viewModel.IsClientConnected, cancellationToken);
		client.Close();
		await WaitUntilTickedAsync(window, viewModel => !viewModel.IsClientConnected, cancellationToken);
	}

	[AvaloniaFact]
	public async Task Events_SnapRefusesARequest_ShowsTheTextInRed()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		using var window = await EmulatorWindowHarness.CreateAsync();
		var intDb = window.Plc.Layout.IntDb;
		var blockSize = window.Plc.Blocks.Single(block => block.DbNumber == intDb.DbNumber).Size;
		using var client = new Plc(CpuType.S71500, "127.0.0.1", window.Plc.Port, 0, 1);
		await client.OpenAsync(cancellationToken);

		var tooLong = new byte[blockSize + 1];
		var write = () => client.WriteBytesAsync(DataType.DataBlock, intDb.DbNumber, 0, tooLong, cancellationToken);
		await write.Should().ThrowAsync<PlcException>();
		await WaitUntilTickedAsync(window, viewModel => viewModel.Message.IsError, cancellationToken);
	}

	private static async Task<bool[]> ButtonStates(MainWindowViewModel viewModel)
	{
		return
		[
			await CanExecute(viewModel.Start),
			await CanExecute(viewModel.Stop),
			await CanExecute(viewModel.Continue)
		];
	}

	private static async Task<bool> CanExecute(ReactiveCommand<Unit, Unit> command)
	{
		return await command.CanExecute.FirstAsync();
	}

	private static Task WaitUntilTickedAsync(
		EmulatorWindowHarness window,
		Func<MainWindowViewModel, bool> reached,
		CancellationToken cancellationToken)
	{
		return TestHelpers.WaitUntilAsync(
			() =>
			{
				window.Engine.Loop.Tick();
				return reached(window.ViewModel);
			},
			_waitLimit,
			TestHelpers.DefaultPollInterval,
			cancellationToken);
	}

	private static void WriteIntCurrentSize(EmulatorWindowHarness window, int currentSize)
	{
		var intDb = window.Plc.Layout.IntDb;
		window.WriteInt(intDb.DbNumber, intDb.CurrentSizeOffset, currentSize);
	}

	private static string Text(int number)
	{
		return number.ToString(CultureInfo.InvariantCulture);
	}
}
