using System.Net;
using System.Net.Sockets;

using FluentResults;

using SemiStep.Core.Configuration;
using SemiStep.Core.Plc.Configuration;
using SemiStep.Core.Recipes;

namespace SemiStep.Tools.PlcEmulator;

/// <summary>The memory of one preset served over S7 on 127.0.0.1; every access to a block holds its area lock.</summary>
internal sealed class EmulatedPlc : IDisposable
{
	private readonly Snap7Server _server;
	private readonly PlcMemory _memory;

	private EmulatedPlc(Snap7Server server, PlcMemory memory, PlcProtocolLayout layout, int stringMaxChars, int port)
	{
		_server = server;
		_memory = memory;
		Layout = layout;
		StringMaxChars = stringMaxChars;
		Port = port;
	}

	public IReadOnlyList<DataBlockInfo> Blocks => _memory.Blocks;

	public PlcProtocolLayout Layout { get; }

	public int StringMaxChars { get; }

	public int Port { get; }

	public string Endpoint => $"127.0.0.1:{Port}";

	public static Result<EmulatedPlc> Start(AppConfiguration configuration, EmulatorOptions options)
	{
		var layout = configuration.PlcConfiguration.Layout;
		var stringMaxChars = new RecipeMetadataRegistry(configuration).GetStringMaxLength();
		var memory = new PlcMemory(layout, stringMaxChars, options);

		// snap7 binds the port with address reuse, so a second server on a busy port starts without an error.
		if (!IsPortFree(options.Port))
		{
			return Result.Fail($"Port {options.Port} is already in use.");
		}

		Snap7Server? server = null;
		try
		{
			server = new Snap7Server(options.Port);
			foreach (var block in memory.Blocks)
			{
				server.RegisterDataBlock(block.DbNumber, memory.BufferOf(block.DbNumber));
			}

			server.Start();
		}
		catch (InvalidOperationException ex)
		{
			server?.Dispose();
			return Result.Fail($"Cannot serve port {options.Port}: {ex.Message}");
		}

		return Result.Ok(new EmulatedPlc(server, memory, layout, stringMaxChars, options.Port));
	}

	public void Dispose()
	{
		_server.Dispose();
	}

	public byte[] Read(int dbNumber, int offset, int count)
	{
		using var areaLock = _server.Lock(dbNumber);
		return _memory.BufferOf(dbNumber).AsSpan(offset, count).ToArray();
	}

	/// <summary>Copies whole blocks under one hold of all their locks, so no client write lands between them.</summary>
	public IReadOnlyDictionary<int, byte[]> ReadBlocks(params int[] dbNumbers)
	{
		var locks = new List<Snap7Server.AreaLock>(dbNumbers.Length);
		try
		{
			foreach (var dbNumber in dbNumbers)
			{
				locks.Add(_server.Lock(dbNumber));
			}

			return dbNumbers.ToDictionary(dbNumber => dbNumber, dbNumber => _memory.BufferOf(dbNumber).ToArray());
		}
		finally
		{
			foreach (var areaLock in locks)
			{
				areaLock.Dispose();
			}
		}
	}

	public void Write(int dbNumber, int offset, ReadOnlySpan<byte> data)
	{
		using var areaLock = _server.Lock(dbNumber);
		data.CopyTo(_memory.BufferOf(dbNumber).AsSpan(offset));
	}

	public IReadOnlyList<EmulatorEvent> PickEvents()
	{
		return _server.PickEvents();
	}

	private static bool IsPortFree(int port)
	{
		var listener = new TcpListener(IPAddress.Loopback, port);
		try
		{
			listener.Start();
			return true;
		}
		catch (SocketException)
		{
			return false;
		}
		finally
		{
			listener.Stop();
		}
	}
}
