namespace Core.Entities;

internal static class MandatoryColumns
{
	internal static ColumnId Action { get; } = new("action");
	internal static ColumnId Task { get; } = new("task");
	internal static ColumnId StepDuration { get; } = new("step_duration");
	internal static ColumnId StepStartTime { get; } = new("step_start_time");
	internal static ColumnId Comment { get; } = new("comment");
}
