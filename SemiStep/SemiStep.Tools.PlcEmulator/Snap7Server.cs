using System.Runtime.InteropServices;
using System.Text;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>One native snap7 server on the loopback address; it keeps the registered buffers referenced until disposal.</summary>
internal sealed class Snap7Server : IDisposable
{
	private const string LoopbackAddress = "127.0.0.1";
	private const int EventTextLength = 256;

	// The return code of snap7's start event is not a status: it read 0x800 in one of four starts.
	private const uint ServerStartedEventCode = 0x00000001;
	private const uint ClientAddedEventCode = 0x00000008;
	private const uint ClientDisconnectedEventCode = 0x00000080;
	private const uint DataWriteEventCode = 0x00040000;

	private readonly List<byte[]> _registeredBuffers = [];
	private nint _handle;
	private bool _started;

	public Snap7Server(int port)
	{
		_handle = Snap7Native.Srv_Create();
		if (_handle == 0)
		{
			throw new InvalidOperationException("snap7 failed to create a server.");
		}

		try
		{
			var localPort = (ushort)port;
			Check(Snap7Native.Srv_SetParam(_handle, Snap7Native.LocalPortParameter, ref localPort), "set the local port");
		}
		catch
		{
			Snap7Native.Srv_Destroy(ref _handle);
			throw;
		}
	}

	public void Dispose()
	{
		if (_handle == 0)
		{
			return;
		}

		if (_started)
		{
			_ = Snap7Native.Srv_Stop(_handle);
		}

		Snap7Native.Srv_Destroy(ref _handle);
		_registeredBuffers.Clear();
	}

	/// <summary>The buffer must come from <c>GC.AllocateArray(pinned: true)</c>: snap7 keeps its address.</summary>
	public void RegisterDataBlock(int dbNumber, byte[] buffer)
	{
		_registeredBuffers.Add(buffer);
		Check(
			Snap7Native.Srv_RegisterArea(
				_handle,
				Snap7Native.DataBlockArea,
				(ushort)dbNumber,
				Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0),
				buffer.Length),
			$"register DB{dbNumber}");
	}

	public void Start()
	{
		Check(Snap7Native.Srv_StartTo(_handle, LoopbackAddress), "start listening");
		_started = true;
	}

	public AreaLock Lock(int dbNumber)
	{
		return new AreaLock(_handle, dbNumber);
	}

	public IReadOnlyList<EmulatorEvent> PickEvents()
	{
		var events = new List<EmulatorEvent>();
		var serverEvent = default(ServerEvent);
		while (true)
		{
			var ready = 0;
			_ = Snap7Native.Srv_PickEvent(_handle, ref serverEvent, ref ready);
			if (ready == 0)
			{
				return events;
			}

			if (Classify(serverEvent) is { } classified)
			{
				events.Add(classified);
			}
		}
	}

	private static EmulatorEvent? Classify(ServerEvent serverEvent)
	{
		switch (serverEvent.Code)
		{
			case ClientAddedEventCode:
				return new ClientConnected();
			case ClientDisconnectedEventCode:
				return new ClientDisconnected();
			case ServerStartedEventCode:
				return null;
		}

		if (serverEvent.ReturnCode == 0)
		{
			return serverEvent.Code == DataWriteEventCode ? new DataWritten(serverEvent.Parameter2) : null;
		}

		var text = new byte[EventTextLength];
		_ = Snap7Native.Srv_EventText(ref serverEvent, text, text.Length);
		return new RequestRefused(Encoding.ASCII.GetString(text).TrimEnd('\0', ' ', '\r', '\n'));
	}

	private static void Check(int errorCode, string action)
	{
		if (errorCode != 0)
		{
			throw new InvalidOperationException($"snap7 failed to {action} (error code 0x{errorCode:X}).");
		}
	}

	internal readonly struct AreaLock : IDisposable
	{
		private readonly nint _handle;
		private readonly int _dbNumber;

		public AreaLock(nint handle, int dbNumber)
		{
			_handle = handle;
			_dbNumber = dbNumber;
			Check(Snap7Native.Srv_LockArea(handle, Snap7Native.DataBlockArea, (ushort)dbNumber), $"lock DB{dbNumber}");
		}

		public void Dispose()
		{
			_ = Snap7Native.Srv_UnlockArea(_handle, Snap7Native.DataBlockArea, (ushort)_dbNumber);
		}
	}
}
