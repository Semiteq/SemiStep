using System.Reactive.Linq;

namespace SemiStep.UI.Coordinator;

internal static class ObservableBoolExtensions
{
	internal static IObservable<bool> AndAlso(this IObservable<bool> first, IObservable<bool> second)
	{
		return first.CombineLatest(second, (left, right) => left && right);
	}
}
