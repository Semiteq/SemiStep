namespace SemiStep.Core.Plc.S7;

/// <summary>Raised by <see cref="S7Driver"/> without a PLC instance.</summary>
internal sealed class DriverNotConnectedException() : Exception("Not connected to PLC");
