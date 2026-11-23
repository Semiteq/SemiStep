using System.Globalization;

using ReactiveUI;

namespace SemiStep.Tools.PlcEmulator.Ui;

internal sealed class MemoryRowViewModel(MemoryField memoryField, string value) : ReactiveObject
{
	private static readonly TimeSpan _flashDuration = TimeSpan.FromSeconds(1);

	private DateTimeOffset _changedAt = DateTimeOffset.MinValue;

	public MemoryField Field { get; } = memoryField;

	public string Offset { get; } = $"{memoryField.Offset.ToString(CultureInfo.InvariantCulture)}.0";

	public string Name => Field.Name;

	public string TypeName => Field.Type.ToString();

	public string Value
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	} = value;

	public bool IsChanged
	{
		get;
		private set => this.RaiseAndSetIfChanged(ref field, value);
	}

	/// <summary>Set by the view while the value cell is being edited.</summary>
	public bool IsEditing { get; set; }

	public void Update(string current, DateTimeOffset now)
	{
		if (!IsEditing && current != Value)
		{
			Value = current;
			_changedAt = now;
		}

		IsChanged = now - _changedAt < _flashDuration;
	}
}
