namespace Domain.Ports;

public interface IPlcConnection : IAsyncDisposable
{
	bool IsConnected { get; }

	Task ConnectAsync(CancellationToken cancellationToken = default);

	Task DisconnectAsync(CancellationToken cancellationToken = default);

	Task<Recipe.Entities.Recipe> ReadRecipeAsync(CancellationToken cancellationToken = default);

	Task WriteRecipeAsync(Recipe.Entities.Recipe recipe, CancellationToken cancellationToken = default);

	Task<PlcStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed record PlcStatus(
	bool IsProcessing,
	bool IsRecipeValid,
	int CurrentStep,
	ExecutionState ExecutionState);

public enum ExecutionState
{
	Stopped,
	Running,
	Paused,
	Error
}
