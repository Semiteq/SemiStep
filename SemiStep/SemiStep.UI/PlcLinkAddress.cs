namespace SemiStep.UI;

/// <summary>The PLC endpoint the link slot names; IsOverride is true when --plc-endpoint differs from the preset.</summary>
public sealed record PlcLinkAddress(string Host, int Port, bool IsOverride);
