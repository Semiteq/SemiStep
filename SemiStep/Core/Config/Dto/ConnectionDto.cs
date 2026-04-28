using YamlDotNet.Serialization;

namespace Config.Dto;

internal sealed class ConnectionDto
{
	[YamlMember(Alias = "connection_file_version")]
	public string? ConnectionFileVersion { get; set; }

	[YamlMember(Alias = "ip")]
	public string? Ip { get; set; }

	[YamlMember(Alias = "connection_protocol")]
	public string? ConnectionProtocol { get; set; }

	[YamlMember(Alias = "plc_rack")]
	public int? PlcRack { get; set; }

	[YamlMember(Alias = "plc_slot")]
	public int? PlcSlot { get; set; }

	[YamlMember(Alias = "max_retries_attempts")]
	public int? MaxRetriesAttempts { get; set; }

	[YamlMember(Alias = "polling_interval_ms")]
	public int? PollingIntervalMs { get; set; }

	[YamlMember(Alias = "writing_timeout_ms")]
	public int? WritingTimeoutMs { get; set; }

	[YamlMember(Alias = "commit_timeout_ms")]
	public int? CommitTimeoutMs { get; set; }

	[YamlMember(Alias = "keep_alive_interval_ms")]
	public int? KeepAliveIntervalMs { get; set; }

	[YamlMember(Alias = "managing_db_number")]
	public int? ManagingDbNumber { get; set; }

	[YamlMember(Alias = "committed_offset")]
	public int? CommittedOffset { get; set; }

	[YamlMember(Alias = "recipe_lines_offset")]
	public int? RecipeLinesOffset { get; set; }

	[YamlMember(Alias = "managing_db_total_size")]
	public int? ManagingDbTotalSize { get; set; }

	[YamlMember(Alias = "int_db_number")]
	public int? IntDbNumber { get; set; }

	[YamlMember(Alias = "int_db_total_capacity_offset")]
	public int? IntDbTotalCapacityOffset { get; set; }

	[YamlMember(Alias = "int_db_current_size_offset")]
	public int? IntDbCurrentSizeOffset { get; set; }

	[YamlMember(Alias = "int_db_data_offset")]
	public int? IntDbDataOffset { get; set; }

	[YamlMember(Alias = "float_db_number")]
	public int? FloatDbNumber { get; set; }

	[YamlMember(Alias = "float_db_total_capacity_offset")]
	public int? FloatDbTotalCapacityOffset { get; set; }

	[YamlMember(Alias = "float_db_current_size_offset")]
	public int? FloatDbCurrentSizeOffset { get; set; }

	[YamlMember(Alias = "float_db_data_offset")]
	public int? FloatDbDataOffset { get; set; }

	[YamlMember(Alias = "string_db_number")]
	public int? StringDbNumber { get; set; }

	[YamlMember(Alias = "string_db_total_capacity_offset")]
	public int? StringDbTotalCapacityOffset { get; set; }

	[YamlMember(Alias = "string_db_current_size_offset")]
	public int? StringDbCurrentSizeOffset { get; set; }

	[YamlMember(Alias = "string_db_data_offset")]
	public int? StringDbDataOffset { get; set; }

	[YamlMember(Alias = "execution_db_number")]
	public int? ExecutionDbNumber { get; set; }

	[YamlMember(Alias = "recipe_active_offset")]
	public int? RecipeActiveOffset { get; set; }

	[YamlMember(Alias = "actual_line_offset")]
	public int? ActualLineOffset { get; set; }

	[YamlMember(Alias = "step_current_time_offset")]
	public int? StepCurrentTimeOffset { get; set; }

	[YamlMember(Alias = "for_loop_count1_offset")]
	public int? ForLoopCount1Offset { get; set; }

	[YamlMember(Alias = "for_loop_count2_offset")]
	public int? ForLoopCount2Offset { get; set; }

	[YamlMember(Alias = "for_loop_count3_offset")]
	public int? ForLoopCount3Offset { get; set; }

	[YamlMember(Alias = "execution_db_total_size")]
	public int? ExecutionDbTotalSize { get; set; }
}
