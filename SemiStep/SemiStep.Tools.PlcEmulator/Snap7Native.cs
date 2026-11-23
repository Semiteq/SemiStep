using System.Runtime.InteropServices;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>The snap7 server functions the emulator needs; the DLL is committed in native/.</summary>
internal static partial class Snap7Native
{
	public const int DataBlockArea = 5;
	public const int LocalPortParameter = 1;

	[LibraryImport("snap7")]
	public static partial nint Srv_Create();

	[LibraryImport("snap7")]
	public static partial void Srv_Destroy(ref nint server);

	[LibraryImport("snap7")]
	public static partial int Srv_SetParam(nint server, int parameter, ref ushort value);

	[LibraryImport("snap7")]
	public static partial int Srv_RegisterArea(nint server, int area, ushort index, nint data, int size);

	[LibraryImport("snap7", StringMarshalling = StringMarshalling.Utf8)]
	public static partial int Srv_StartTo(nint server, string address);

	[LibraryImport("snap7")]
	public static partial int Srv_Stop(nint server);

	[LibraryImport("snap7")]
	public static partial int Srv_LockArea(nint server, int area, ushort index);

	[LibraryImport("snap7")]
	public static partial int Srv_UnlockArea(nint server, int area, ushort index);

	[LibraryImport("snap7")]
	public static partial int Srv_PickEvent(nint server, ref ServerEvent serverEvent, ref int ready);

	[LibraryImport("snap7")]
	public static partial int Srv_EventText(ref ServerEvent serverEvent, byte[] text, int textLength);
}
