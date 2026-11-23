using System.Buffers.Binary;

using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>The data blocks of the emulated PLC, allocated pinned for snap7.</summary>
internal sealed class PlcMemory
{
	private readonly List<(DataBlockInfo Info, byte[] Buffer)> _blocks = [];

	public PlcMemory(PlcProtocolLayout layout, int stringMaxChars, EmulatorOptions options)
	{
		var stringElementSize =
			new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, stringMaxChars).WStringElementSize;

		AddBlock("managing", layout.ManagingDb.DbNumber, layout.ManagingDb.TotalSize);
		AddBlock("execution", layout.ExecutionDb.DbNumber, layout.ExecutionDb.TotalSize);
		AddDataBlock("int", layout.IntDb, options.IntCapacity, ProtocolConstants.IntElementSize);
		AddDataBlock("float", layout.FloatDb, options.FloatCapacity, ProtocolConstants.FloatElementSize);
		AddDataBlock("string", layout.StringDb, options.StringCapacity, stringElementSize);

		BinaryPrimitives.WriteInt32BigEndian(
			BufferOf(layout.ManagingDb.DbNumber).AsSpan(layout.ManagingDb.VersionOffset),
			ProtocolConstants.ProtocolVersion);
	}

	public IReadOnlyList<DataBlockInfo> Blocks => [.. _blocks.Select(block => block.Info)];

	public byte[] BufferOf(int dbNumber)
	{
		return _blocks.Single(block => block.Info.DbNumber == dbNumber).Buffer;
	}

	private void AddDataBlock(string name, DataDbLayout layout, int capacity, int elementSize)
	{
		AddBlock(name, layout.DbNumber, layout.DataStartOffset + (capacity * elementSize));
		BinaryPrimitives.WriteUInt32BigEndian(BufferOf(layout.DbNumber).AsSpan(layout.CapacityOffset), (uint)capacity);
	}

	private void AddBlock(string name, int dbNumber, int size)
	{
		_blocks.Add((new DataBlockInfo(name, dbNumber, size), GC.AllocateArray<byte>(size, pinned: true)));
	}
}
