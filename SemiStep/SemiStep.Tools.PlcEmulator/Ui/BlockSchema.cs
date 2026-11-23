using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Plc.Configuration.Memory;
using SemiStep.Core.Plc.S7.Protocol;
using SemiStep.Core.Plc.S7.Serialization;

namespace SemiStep.Tools.PlcEmulator.Ui;

internal sealed class BlockSchema
{
	private readonly ArrayShape? _array;

	private BlockSchema(DataBlockInfo info, string title, IReadOnlyList<MemoryField> fixedFields, ArrayShape? array)
	{
		Info = info;
		Title = title;
		FixedFields = fixedFields;
		_array = array;
	}

	public DataBlockInfo Info { get; }

	public string Title { get; }

	public IReadOnlyList<MemoryField> FixedFields { get; }

	public int AllocatedCapacity =>
		_array is null ? 0 : (Info.Size - _array.Layout.DataStartOffset) / _array.ElementSize;

	public static IReadOnlyList<BlockSchema> For(
		IReadOnlyList<DataBlockInfo> blocks,
		PlcProtocolLayout layout,
		int stringMaxChars)
	{
		var stringElementSize = new ArrayCodec(layout.IntDb, layout.FloatDb, layout.StringDb, stringMaxChars)
			.WStringElementSize;
		var managing = layout.ManagingDb;
		var execution = layout.ExecutionDb;
		return
		[
			FixedBlock(
				BlockOf(blocks, managing.DbNumber),
				"Managing",
				(managing.VersionOffset, "version", FieldType.DInt),
				(managing.CommittedOffset, "committed", FieldType.Bool),
				(managing.RecipeLinesOffset, "recipe_lines", FieldType.DInt)),
			FixedBlock(
				BlockOf(blocks, execution.DbNumber),
				"Execution",
				(execution.RecipeActiveOffset, "recipe_active", FieldType.Bool),
				(execution.ActualLineOffset, "actual_line", FieldType.DInt),
				(execution.StepCurrentTimeOffset, "step_current_time", FieldType.Real),
				(execution.ForLoopCount1Offset, "for_loop_count1", FieldType.DInt),
				(execution.ForLoopCount2Offset, "for_loop_count2", FieldType.DInt),
				(execution.ForLoopCount3Offset, "for_loop_count3", FieldType.DInt)),
			ArrayBlock(blocks, "Int array", layout.IntDb, FieldType.DInt, ProtocolConstants.IntElementSize),
			ArrayBlock(blocks, "Float array", layout.FloatDb, FieldType.Real, ProtocolConstants.FloatElementSize),
			ArrayBlock(blocks, "String array", layout.StringDb, FieldType.WString, stringElementSize)
		];
	}

	private static BlockSchema FixedBlock(
		DataBlockInfo info,
		string title,
		params (int Offset, string Name, FieldType Type)[] fields)
	{
		return new BlockSchema(
			info,
			title,
			[.. fields.Select(field => new MemoryField(info.DbNumber, field.Offset, field.Name, field.Type))],
			null);
	}

	private static BlockSchema ArrayBlock(
		IReadOnlyList<DataBlockInfo> blocks,
		string title,
		DataDbLayout layout,
		FieldType elementType,
		int elementSize)
	{
		var info = BlockOf(blocks, layout.DbNumber);
		return new BlockSchema(
			info,
			title,
			[
				new MemoryField(info.DbNumber, layout.CapacityOffset, "capacity", FieldType.DInt),
				new MemoryField(info.DbNumber, layout.CurrentSizeOffset, "current_size", FieldType.DInt)
			],
			new ArrayShape(layout, elementType, elementSize));
	}

	private static DataBlockInfo BlockOf(IReadOnlyList<DataBlockInfo> blocks, int dbNumber)
	{
		return blocks.Single(block => block.DbNumber == dbNumber);
	}

	public int ReadCurrentSize(byte[] snapshot)
	{
		return _array is null ? 0 : ArrayCodec.ReadArrayCurrentSize(snapshot, _array.Layout);
	}

	public int ShownElements(int currentSize)
	{
		return Math.Clamp(currentSize, 0, AllocatedCapacity);
	}

	public IEnumerable<MemoryField> Elements(int fromIndex, int toIndex)
	{
		if (_array is null)
		{
			yield break;
		}

		for (var index = fromIndex; index < toIndex; index++)
		{
			var offset = _array.Layout.DataStartOffset + (index * _array.ElementSize);
			yield return new MemoryField(Info.DbNumber, offset, $"data[{index}]", _array.ElementType);
		}
	}

	private sealed record ArrayShape(DataDbLayout Layout, FieldType ElementType, int ElementSize);
}
