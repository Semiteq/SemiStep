using System.Runtime.InteropServices;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>The snap7 TSrvEvent record; a non-zero return code marks a request the server refused.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct ServerEvent
{
	public long Time;
	public int Sender;
	public uint Code;
	public ushort ReturnCode;
	public ushort Parameter1;
	public ushort Parameter2;
	public ushort Parameter3;
	public ushort Parameter4;
}
