namespace SemiStep.Tests.S7.Helpers;

internal sealed class TransportHold(int dbNumber, int heldCallNumber, TaskCreationOptions releaseOptions)
{
	private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly TaskCompletionSource _release = new(releaseOptions);
	private int _calls;

	public Task Reached => _reached.Task;

	public CancellationToken HeldToken { get; private set; }

	/// <summary>The held call ignores its cancellation token.</summary>
	public Task OnCall(int callDbNumber, CancellationToken ct)
	{
		if (callDbNumber != dbNumber || Interlocked.Increment(ref _calls) != heldCallNumber)
		{
			return Task.CompletedTask;
		}

		HeldToken = ct;
		_reached.SetResult();

		return _release.Task;
	}

	public void Release()
	{
		_release.SetResult();
	}

	public void Fail(Exception exception)
	{
		_release.SetException(exception);
	}
}
