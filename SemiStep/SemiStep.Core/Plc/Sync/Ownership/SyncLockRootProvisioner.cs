using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SemiStep.Core.Plc.Sync.Ownership;

internal sealed class SyncLockRootProvisioner
{
	public void EnsureRoot(string lockRoot)
	{
		var directory = new DirectoryInfo(lockRoot);

		if (directory.Exists)
		{
			return;
		}

		directory.Create();

		if (OperatingSystem.IsWindows())
		{
			TryGrantUsersModify(directory);
		}
	}

	[SupportedOSPlatform("windows")]
	private static void TryGrantUsersModify(DirectoryInfo directory)
	{
		try
		{
			var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, domainSid: null);
			var rule = new FileSystemAccessRule(
				usersSid,
				FileSystemRights.Modify,
				InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
				PropagationFlags.None,
				AccessControlType.Allow);

			var security = directory.GetAccessControl();
			security.AddAccessRule(rule);
			directory.SetAccessControl(security);
		}
		catch (Exception exception) when (exception is UnauthorizedAccessException
			or IOException
			or PlatformNotSupportedException)
		{
			// Best-effort ACL provisioning. A different Windows user that cannot open the
			// lock file is mapped to a clean refusal in FileSyncOwnership.TryAcquire,
			// so a failure to widen the ACL here does not crash the caller. Installer-time
			// provisioning is documented in the plan's Post-Completion section.
		}
	}
}
