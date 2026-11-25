using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;

using ReactiveUI;

using SemiStep.Core.Plc.State;

using SemiStep.UI.Coordinator;

namespace SemiStep.UI.Plc;

/// <summary>The status-bar link slot: one state over the PLC snapshots and the sync toggle's execution.</summary>
public sealed class PlcLinkViewModel : ReactiveObject, IDisposable
{
	private readonly CompositeDisposable _disposables = [];
	private readonly ObservableAsPropertyHelper<PlcLinkView> _view;

	public PlcLinkViewModel(
		RecipeCoordinator coordinator,
		PlcLinkAddress address,
		IObservable<SyncToggleDirection> syncToggle,
		IScheduler scheduler)
	{
		var latchedSnapshots = coordinator.PlcStateChanged
			.StartWith(new PlcSessionSnapshot(
				coordinator.IsConnected ? PlcConnectionState.Connected : PlcConnectionState.Disconnected,
				coordinator.SyncStatus,
				coordinator.IsSyncEnabled))
			.Scan(
				new LatchedSnapshot(PlcSessionSnapshot.InitialState, IsErrorLatched: false),
				(previous, snapshot) => new LatchedSnapshot(snapshot, Latch(previous.IsErrorLatched, snapshot.SyncStatus)));

		var toggleDirection = syncToggle
			.StartWith(SyncToggleDirection.None)
			.DistinctUntilChanged();

		var ageTicks = Observable.Interval(TimeSpan.FromSeconds(1), scheduler)
			.Select(_ => Unit.Default)
			.StartWith(Unit.Default);

		_view = latchedSnapshots
			.CombineLatest(toggleDirection, ageTicks, (latched, direction, _) => (latched, direction))
			.Select(input => PlcLinkView.Build(
				Resolve(input.latched, input.direction),
				address,
				coordinator.LastExchangeTime,
				scheduler.Now))
			.ToProperty(this, x => x.View, initialValue: PlcLinkView.Build(
				PlcLinkKind.SyncOff, address, lastExchange: null, scheduler.Now))
			.DisposeWith(_disposables);
	}

	public PlcLinkView View => _view.Value;

	public void Dispose()
	{
		_disposables.Dispose();
	}

	private static bool Latch(bool isErrorLatched, PlcSyncStatus status)
	{
		return status switch
		{
			PlcSyncStatus.Failed => true,
			PlcSyncStatus.Synced or PlcSyncStatus.Idle => false,
			_ => isErrorLatched
		};
	}

	private static PlcLinkKind Resolve(LatchedSnapshot latched, SyncToggleDirection direction)
	{
		var snapshot = latched.Snapshot;

		return direction switch
		{
			SyncToggleDirection.Disabling when snapshot.ConnectionState != PlcConnectionState.Disconnected
				=> PlcLinkKind.Disconnecting,
			SyncToggleDirection.Enabling when snapshot.IsSyncEnabled => PlcLinkKind.Connecting,
			_ when !snapshot.IsSyncEnabled => PlcLinkKind.SyncOff,
			_ when snapshot.ConnectionState != PlcConnectionState.Connected => PlcLinkKind.NoLink,
			_ when latched.IsErrorLatched => PlcLinkKind.SyncError,
			_ => PlcLinkKind.Connected
		};
	}

	private sealed record LatchedSnapshot(PlcSessionSnapshot Snapshot, bool IsErrorLatched);
}
