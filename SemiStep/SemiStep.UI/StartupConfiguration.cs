using SemiStep.Core.Configuration;

namespace SemiStep.UI;

/// <summary>The loaded configuration with the PLC endpoint override applied, and the address the status bar names.</summary>
public sealed record StartupConfiguration(AppConfiguration Configuration, PlcLinkAddress LinkAddress);
