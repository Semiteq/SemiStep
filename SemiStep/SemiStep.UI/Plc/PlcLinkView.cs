using System.Globalization;

using SemiStep.UI.Localization;

namespace SemiStep.UI.Plc;

public enum PlcLinkKind
{
	Disconnecting,
	SyncOff,
	Connecting,
	NoLink,
	SyncError,
	Connected
}

/// <summary>What the link slot shows: the dot, the bar text and the tooltip.</summary>
public sealed record PlcLinkView(PlcLinkKind Kind, string Text, string ToolTip)
{
	public bool IsGreenDot => Kind == PlcLinkKind.Connected;

	public bool IsRedDot => Kind is PlcLinkKind.NoLink or PlcLinkKind.SyncError;

	public bool IsHollowDot => Kind is PlcLinkKind.Connecting or PlcLinkKind.Disconnecting;

	public static PlcLinkView Build(
		PlcLinkKind kind,
		PlcLinkAddress address,
		DateTimeOffset? lastExchange,
		DateTimeOffset now)
	{
		var age = lastExchange is null ? Resources.LinkNever : FormatAge(now - lastExchange.Value);
		var text = kind switch
		{
			PlcLinkKind.Disconnecting => Resources.LinkDisconnecting,
			PlcLinkKind.SyncOff => Resources.LinkSyncOff,
			PlcLinkKind.Connecting => Resources.LinkConnecting,
			PlcLinkKind.NoLink => string.Format(CultureInfo.InvariantCulture, Resources.LinkNoLinkFormat, age),
			PlcLinkKind.SyncError => Resources.LinkSyncError,
			_ => Resources.LinkConnected
		};

		if (address.IsOverride && kind != PlcLinkKind.NoLink)
		{
			text = $"{text}, {address.Host}";
		}

		var toolTip = string.Format(CultureInfo.InvariantCulture, Resources.LinkAddressFormat, address.Host, address.Port)
			+ Environment.NewLine
			+ string.Format(CultureInfo.InvariantCulture, Resources.LinkLastExchangeFormat, age);

		return new PlcLinkView(kind, text, toolTip);
	}

	private static string FormatAge(TimeSpan age)
	{
		return age.TotalSeconds switch
		{
			>= 3600 => Resources.LinkAgeOverHour,
			>= 60 => Resources.LinkAgeOverMinute,
			>= 30 => Resources.LinkAgeOver30Seconds,
			_ => string.Format(
				CultureInfo.InvariantCulture,
				Resources.LinkAgeSecondsFormat,
				Math.Max(0, (int)age.TotalSeconds))
		};
	}
}
