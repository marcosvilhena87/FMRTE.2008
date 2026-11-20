namespace FMRTE2008.Application;

/// <summary>
/// Identity obtained from the acquired process HANDLE, not by reopening a PID.
/// StartTimeUtcTicks is the UTC DateTime ticks of the kernel creation time.
/// </summary>
public sealed record ProcessHandleIdentity(int ProcessId, long StartTimeUtcTicks);

/// <summary>
/// Optional platform contract used by the F10 strict opening gate.
/// Implementations must query the same acquired handle, and throw when the
/// handle is closed, inaccessible or its process has exited.
/// </summary>
public interface IProcessHandleBoundReadSession : IProcessReadSession
{
    ProcessHandleIdentity GetLiveHandleIdentity();
}
