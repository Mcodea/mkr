using System.Security.Principal;
using Mkr.Core;

namespace Mkr.Desktop;

/// <summary>Identity of the Windows session: domain account, security identifier and computer name. Nothing is asked of the user.</summary>
internal sealed class WindowsIdentityProvider : IIdentityProvider
{
    public UserIdentity Current
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using var identity = WindowsIdentity.GetCurrent();
                    return new UserIdentity(identity.Name, identity.User?.Value ?? "SID-indisponible", Environment.MachineName);
                }
                catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException)
                {
                    // Fall through: the audit log must still name an account rather than fail the action.
                }
            }
            return new EnvironmentIdentityProvider().Current;
        }
    }
}
