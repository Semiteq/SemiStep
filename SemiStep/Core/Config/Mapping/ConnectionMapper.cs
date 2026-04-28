using Config.Dto;

using TypesShared.Plc;
using TypesShared.Plc.Memory;

namespace Config.Mapping;

internal static class ConnectionMapper
{
	public static PlcConfiguration Map(ConnectionDto? dto)
	{
		if (dto is null)
		{
			return PlcConfiguration.Default;
		}

		var connection = MapConnection(dto);
		var protocolSettings = MapProtocolSettings(dto);
		var layout = MapLayout(dto);

		return new PlcConfiguration(connection, protocolSettings, layout);
	}

	private static PlcConnectionSettings MapConnection(ConnectionDto dto)
	{
		var defaults = PlcConnectionSettings.Default;

		var ipAddress = defaults.IpAddress;
		var port = defaults.Port;

		if (dto.Ip is not null)
		{
			ParseIpAndPort(dto.Ip, out ipAddress, out port);
		}

		return new PlcConnectionSettings(
			IpAddress: ipAddress,
			Port: port,
			Rack: dto.PlcRack ?? defaults.Rack,
			Slot: dto.PlcSlot ?? defaults.Slot);
	}

	private static PlcProtocolSettings MapProtocolSettings(ConnectionDto dto)
	{
		var defaults = PlcProtocolSettings.Default;

		return new PlcProtocolSettings(
			MaxRetryAttempts: dto.MaxRetriesAttempts ?? defaults.MaxRetryAttempts,
			PollingIntervalMs: dto.PollingIntervalMs ?? defaults.PollingIntervalMs,
			WritingTimeoutMs: dto.WritingTimeoutMs ?? defaults.WritingTimeoutMs,
			CommitTimeoutMs: dto.CommitTimeoutMs ?? defaults.CommitTimeoutMs,
			KeepAliveIntervalMs: dto.KeepAliveIntervalMs ?? defaults.KeepAliveIntervalMs);
	}

	private static PlcProtocolLayout MapLayout(ConnectionDto dto)
	{
		return new PlcProtocolLayout(
			ManagingDb: MapManagingDb(dto),
			IntDb: MapDataDb(dto.IntDbNumber, dto.IntDbTotalCapacityOffset, dto.IntDbCurrentSizeOffset,
				dto.IntDbDataOffset, DataDbLayout.DefaultInt),
			FloatDb: MapDataDb(dto.FloatDbNumber, dto.FloatDbTotalCapacityOffset, dto.FloatDbCurrentSizeOffset,
				dto.FloatDbDataOffset, DataDbLayout.DefaultFloat),
			StringDb: MapDataDb(dto.StringDbNumber, dto.StringDbTotalCapacityOffset, dto.StringDbCurrentSizeOffset,
				dto.StringDbDataOffset, DataDbLayout.DefaultString),
			ExecutionDb: MapExecutionDb(dto));
	}

	private static ManagingDbLayout MapManagingDb(ConnectionDto dto)
	{
		var defaults = ManagingDbLayout.Default;

		return new ManagingDbLayout(
			DbNumber: dto.ManagingDbNumber ?? defaults.DbNumber,
			CommittedOffset: dto.CommittedOffset ?? defaults.CommittedOffset,
			RecipeLinesOffset: dto.RecipeLinesOffset ?? defaults.RecipeLinesOffset,
			TotalSize: dto.ManagingDbTotalSize ?? defaults.TotalSize);
	}

	private static DataDbLayout MapDataDb(
		int? dbNumber,
		int? capacityOffset,
		int? currentSizeOffset,
		int? dataOffset,
		DataDbLayout defaults)
	{
		return new DataDbLayout(
			DbNumber: dbNumber ?? defaults.DbNumber,
			CapacityOffset: capacityOffset ?? defaults.CapacityOffset,
			CurrentSizeOffset: currentSizeOffset ?? defaults.CurrentSizeOffset,
			DataStartOffset: dataOffset ?? defaults.DataStartOffset);
	}

	private static ExecutionDbLayout MapExecutionDb(ConnectionDto dto)
	{
		var defaults = ExecutionDbLayout.Default;

		return new ExecutionDbLayout(
			DbNumber: dto.ExecutionDbNumber ?? defaults.DbNumber,
			RecipeActiveOffset: dto.RecipeActiveOffset ?? defaults.RecipeActiveOffset,
			ActualLineOffset: dto.ActualLineOffset ?? defaults.ActualLineOffset,
			StepCurrentTimeOffset: dto.StepCurrentTimeOffset ?? defaults.StepCurrentTimeOffset,
			ForLoopCount1Offset: dto.ForLoopCount1Offset ?? defaults.ForLoopCount1Offset,
			ForLoopCount2Offset: dto.ForLoopCount2Offset ?? defaults.ForLoopCount2Offset,
			ForLoopCount3Offset: dto.ForLoopCount3Offset ?? defaults.ForLoopCount3Offset,
			TotalSize: dto.ExecutionDbTotalSize ?? defaults.TotalSize);
	}

	private static void ParseIpAndPort(string ipWithPort, out string ipAddress, out int port)
	{
		var colonIndex = ipWithPort.LastIndexOf(':');

		if (colonIndex > 0 && int.TryParse(ipWithPort[(colonIndex + 1)..], out var parsedPort))
		{
			ipAddress = ipWithPort[..colonIndex];
			port = parsedPort;
		}
		else
		{
			ipAddress = ipWithPort;
			port = 102;
		}
	}
}
