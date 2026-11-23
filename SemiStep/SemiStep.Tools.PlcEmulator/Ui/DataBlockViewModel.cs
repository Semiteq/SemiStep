using System.Collections.ObjectModel;
using System.Globalization;

namespace SemiStep.Tools.PlcEmulator.Ui;

internal sealed class DataBlockViewModel(BlockSchema schema, MemoryFieldCodec codec)
{
	private int? _reportedInvalidSize;

	public BlockSchema Schema { get; } = schema;

	public string Header => $"DB{Schema.Info.DbNumber}";

	public string Title => Schema.Title;

	public string SizeText => $"{Schema.Info.Size.ToString("N0", CultureInfo.InvariantCulture)} bytes";

	public ObservableCollection<MemoryRowViewModel> Rows { get; } = [];

	/// <summary>Returns a warning once per bad <c>current_size</c> value, else null.</summary>
	public string? Apply(byte[] snapshot, DateTimeOffset now)
	{
		var currentSize = Schema.ReadCurrentSize(snapshot);
		ResizeRows(snapshot, Schema.ShownElements(currentSize));

		foreach (var row in Rows)
		{
			row.Update(codec.Format(row.Field, snapshot), now);
		}

		return WarnAboutSize(currentSize);
	}

	private void ResizeRows(byte[] snapshot, int shownElements)
	{
		var fixedCount = Schema.FixedFields.Count;
		if (Rows.Count == 0)
		{
			foreach (var field in Schema.FixedFields)
			{
				Rows.Add(new MemoryRowViewModel(field, codec.Format(field, snapshot)));
			}
		}

		while (Rows.Count - fixedCount > shownElements)
		{
			Rows.RemoveAt(Rows.Count - 1);
		}

		foreach (var element in Schema.Elements(Rows.Count - fixedCount, shownElements))
		{
			Rows.Add(new MemoryRowViewModel(element, codec.Format(element, snapshot)));
		}
	}

	private string? WarnAboutSize(int currentSize)
	{
		var isValid = currentSize >= 0 && currentSize <= Schema.AllocatedCapacity;
		if (isValid)
		{
			_reportedInvalidSize = null;
			return null;
		}

		if (_reportedInvalidSize == currentSize)
		{
			return null;
		}

		_reportedInvalidSize = currentSize;
		return $"{Header}.current_size is {currentSize}, the block holds {Schema.AllocatedCapacity} elements.";
	}
}
