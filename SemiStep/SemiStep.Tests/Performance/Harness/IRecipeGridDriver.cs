using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;

namespace SemiStep.Tests.Performance.Harness;

// User actions driven over the PUBLIC view surface, so one parity scenario body runs against either driver.
public interface IRecipeGridDriver : IAsyncDisposable
{
	// The window hosting the view; the runner reads it as the TopLevel of the measured tree.
	TopLevel Root { get; }

	// The items-panel subtree the runner snapshots visuals over. A strict subset of Root.
	Visual SnapshotScope { get; }

	// Step count as the surface projects it (transposed columns / canonical rows).
	int ItemCount { get; }

	// Ascending step indices the selection model currently holds, as both views feed the surface.
	IReadOnlyList<int> SelectedIndices { get; }

	// Ascending step indices whose containers are realized right now (the virtualized viewport range).
	IReadOnlyList<int> RealizedIndices { get; }

	// Snapshot of the container controls realized right now. The retention survivor probe weak-references
	// these, parks the viewport away, and counts how many the control stack still roots after unrealize.
	IReadOnlyList<Control> RealizedContainers { get; }

	// Transposed drives to a column; canonical drives to the row at the same index.
	Task ScrollToColumnAsync(int index);

	Task AddStepsAsync(int count);

	Task RemoveStepsAsync(int count);

	Task SelectRangeAsync(int from, int to);

	Task WaitForIdleAsync();
}
