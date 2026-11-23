namespace SemiStep.Tools.PlcEmulator;

internal abstract record EmulatorEvent;

internal sealed record RecipeCommitted(int Lines) : EmulatorEvent;

internal sealed record RequestRefused(string Text) : EmulatorEvent;

internal sealed record ClientConnected : EmulatorEvent;

internal sealed record ClientDisconnected : EmulatorEvent;

internal sealed record TickFailed(string Text) : EmulatorEvent;

/// <summary>A client wrote into a data block; the loop consumes it and never raises it.</summary>
internal sealed record DataWritten(int DbNumber) : EmulatorEvent;
