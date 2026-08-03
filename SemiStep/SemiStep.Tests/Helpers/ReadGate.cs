namespace SemiStep.Tests.Helpers;

/// <summary>
/// A deterministic, token-aware gate for a stubbed PLC read. The read signals <see cref="Entered"/>
/// when it reaches the gate, then awaits <see cref="WaitAsync"/> until the test calls <see cref="Release"/>.
/// Because the wait uses <c>Task.WaitAsync(ct)</c>, a cancellation arriving <c>mid-wait</c> makes the
/// gated read throw an <see cref="OperationCanceledException"/> — the exact behavior a bare
/// <c>ct.ThrowIfCancellationRequested()</c> at read entry cannot reproduce.
/// </summary>
public sealed class ReadGate
{
	private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>Completes once the gated read has reached the gate and is awaiting release.</summary>
	public Task Entered => _entered.Task;

	/// <summary>Releases the gated read so it can complete.</summary>
	public void Release()
	{
		_release.TrySetResult();
	}

	/// <summary>
	/// Signals <see cref="Entered"/>, then awaits release while observing <paramref name="ct"/>.
	/// A cancel arriving during the wait throws an <see cref="OperationCanceledException"/>.
	/// </summary>
	public async Task WaitAsync(CancellationToken ct)
	{
		_entered.TrySetResult();
		await _release.Task.WaitAsync(ct);
	}
}
