using FluentAssertions;

using SemiStep.Core.Plc.S7;

using Xunit;

namespace SemiStep.Tests.S7;

[Trait("Component", "S7")]
[Trait("Area", "TransportSerialization")]
[Trait("Category", "Unit")]
public sealed class TransportSerializerTests
{
	private const int ConcurrentOperationCount = 16;

	[Fact]
	public async Task RunAsync_ConcurrentOperations_NeverOverlap()
	{
		using var serializer = new TransportSerializer();
		var currentConcurrency = 0;
		var maxObservedConcurrency = 0;

		async Task TrackedOperation()
		{
			var concurrency = Interlocked.Increment(ref currentConcurrency);
			InterlockedMax(ref maxObservedConcurrency, concurrency);

			await Task.Delay(5);

			Interlocked.Decrement(ref currentConcurrency);
		}

		var operations = Enumerable
			.Range(0, ConcurrentOperationCount)
			.Select(_ => serializer.RunAsync(TrackedOperation, CancellationToken.None));

		await Task.WhenAll(operations);

		maxObservedConcurrency.Should().Be(1);
	}

	[Fact]
	public async Task RunAsync_GenericConcurrentOperations_NeverOverlap()
	{
		using var serializer = new TransportSerializer();
		var currentConcurrency = 0;
		var maxObservedConcurrency = 0;

		async Task<int> TrackedOperation()
		{
			var concurrency = Interlocked.Increment(ref currentConcurrency);
			InterlockedMax(ref maxObservedConcurrency, concurrency);

			await Task.Delay(5);

			return Interlocked.Decrement(ref currentConcurrency);
		}

		var operations = Enumerable
			.Range(0, ConcurrentOperationCount)
			.Select(_ => serializer.RunAsync(TrackedOperation, CancellationToken.None));

		await Task.WhenAll(operations);

		maxObservedConcurrency.Should().Be(1);
	}

	[Fact]
	public async Task RunAsync_PreCancelledToken_ThrowsAndLeavesGateUsable()
	{
		using var serializer = new TransportSerializer();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var act = () => serializer.RunAsync(() => Task.CompletedTask, cts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();

		var subsequentRan = false;
		await serializer.RunAsync(
			() =>
			{
				subsequentRan = true;
				return Task.CompletedTask;
			},
			CancellationToken.None);

		subsequentRan.Should().BeTrue();
	}

	[Fact]
	public async Task RunAsync_TokenCancelledWhileGateHeld_ThrowsAndLeavesGateUsable()
	{
		using var serializer = new TransportSerializer();
		using var cts = new CancellationTokenSource();

		var gateEntered = new TaskCompletionSource();
		var releaseHolder = new TaskCompletionSource();

		var holder = serializer.RunAsync(
			async () =>
			{
				gateEntered.SetResult();
				await releaseHolder.Task;
			},
			CancellationToken.None);

		await gateEntered.Task;

		var waiter = serializer.RunAsync(() => Task.CompletedTask, cts.Token);
		cts.Cancel();

		var act = () => waiter;
		await act.Should().ThrowAsync<OperationCanceledException>();

		releaseHolder.SetResult();
		await holder;

		var subsequentRan = false;
		await serializer.RunAsync(
			() =>
			{
				subsequentRan = true;
				return Task.CompletedTask;
			},
			CancellationToken.None);

		subsequentRan.Should().BeTrue();
	}

	[Fact]
	public async Task RunAsync_OperationThrows_ReleasesGate()
	{
		using var serializer = new TransportSerializer();

		var act = () => serializer.RunAsync(
			() => throw new InvalidOperationException("boom"),
			CancellationToken.None);

		await act.Should().ThrowAsync<InvalidOperationException>();

		var subsequentRan = false;
		await serializer.RunAsync(
			() =>
			{
				subsequentRan = true;
				return Task.CompletedTask;
			},
			CancellationToken.None);

		subsequentRan.Should().BeTrue();
	}

	private static void InterlockedMax(ref int target, int candidate)
	{
		var observed = Volatile.Read(ref target);
		while (candidate > observed)
		{
			var previous = Interlocked.CompareExchange(ref target, candidate, observed);
			if (previous == observed)
			{
				return;
			}

			observed = previous;
		}
	}
}
