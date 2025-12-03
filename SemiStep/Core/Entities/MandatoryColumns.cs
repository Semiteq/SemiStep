namespace Core.Entities;

public static class MandatoryColumns
{
	public static ColumnIdentifier Action { get; } = new("action");
	public static ColumnIdentifier Task { get; } = new("task");
	public static ColumnIdentifier StepDuration { get; } = new("step_duration");
	public static ColumnIdentifier StepStartTime { get; } = new("step_start_time");
	public static ColumnIdentifier Comment { get; } = new("comment");
}
