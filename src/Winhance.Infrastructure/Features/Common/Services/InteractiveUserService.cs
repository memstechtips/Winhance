using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.RemoteDesktop;
using Windows.Win32.System.Threading;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

namespace Winhance.Infrastructure.Features.Common.Services;

[SupportedOSPlatform("windows")]
internal class InteractiveUserService : IInteractiveUserService, IDisposable
{
    private readonly ILogService _logService;
    private readonly IProcessExecutor _processExecutor;

    private readonly bool _isOtsElevation;
    private readonly string? _interactiveUserSid;
    private readonly string _interactiveUserName;
    private readonly string _interactiveUserProfilePath;
    private HANDLE _interactiveUserToken;
    private bool _disposed;

    public bool IsOtsElevation => _isOtsElevation;
    public string? InteractiveUserSid => _interactiveUserSid;
    public string InteractiveUserName => _interactiveUserName;
    public bool HasInteractiveUserToken => !_interactiveUserToken.IsNull;

    public InteractiveUserService(ILogService logService, IProcessExecutor processExecutor)
    {
        _logService = logService;
        _processExecutor = processExecutor;

        var currentSid = WindowsIdentity.GetCurrent().User?.Value;
        string? detectedSid = null;

        detectedSid = TryGetSidFromExplorerToken();

        if (detectedSid == null)
        {
            detectedSid = TryGetSidFromWmi();
        }

        if (detectedSid == null)
        {
            detectedSid = TryGetSidFromWtsSession();
        }

        if (detectedSid != null && !string.Equals(detectedSid, currentSid, StringComparison.OrdinalIgnoreCase))
        {
            _isOtsElevation = true;
            _interactiveUserSid = detectedSid;
            _interactiveUserName = ResolveSidToUsername(detectedSid);
            _interactiveUserProfilePath = ResolveProfilePath(detectedSid);
            _logService.Log(LogLevel.Info,
                $"OTS elevation detected: Interactive user is '{_interactiveUserName}' (SID: {detectedSid}), " +
                $"process running as '{Environment.UserName}'. " +
                $"HKCU registry operations will be redirected to HKU\\{detectedSid}");

            if (!_interactiveUserToken.IsNull)
            {
                _logService.Log(LogLevel.Info,
                    "Interactive user token acquired — WinGet and other processes will run as the interactive user");
            }
            else
            {
                _logService.Log(LogLevel.Warning,
                    "Could not acquire interactive user token — processes will run as the elevated admin user");
            }
        }
        else
        {
            _isOtsElevation = false;
            _interactiveUserSid = null;
            _interactiveUserName = Environment.UserName;
            _interactiveUserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (detectedSid == null)
            {
                _logService.Log(LogLevel.Warning,
                    "Could not determine interactive user identity — assuming current process user");
            }
        }
    }

    public string GetInteractiveUserFolderPath(Environment.SpecialFolder folder)
    {
        if (!_isOtsElevation)
            return Environment.GetFolderPath(folder);

        return folder switch
        {
            Environment.SpecialFolder.LocalApplicationData =>
                Path.Combine(_interactiveUserProfilePath, "AppData", "Local"),
            Environment.SpecialFolder.Programs =>
                Path.Combine(_interactiveUserProfilePath, "AppData", "Roaming",
                    "Microsoft", "Windows", "Start Menu", "Programs"),
            Environment.SpecialFolder.UserProfile =>
                _interactiveUserProfilePath,
            Environment.SpecialFolder.ApplicationData =>
                Path.Combine(_interactiveUserProfilePath, "AppData", "Roaming"),
            // System-wide folders are unaffected by OTS
            _ => Environment.GetFolderPath(folder),
        };
    }

    public async Task<InteractiveProcessResult> RunProcessAsInteractiveUserAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default,
        int timeoutMs = 300_000,
        Action<string>? onProgressLine = null)
    {
        if (!_isOtsElevation || _interactiveUserToken.IsNull)
        {
            return await RunProcessNormalAsync(fileName, arguments, onOutputLine, onErrorLine, cancellationToken, timeoutMs).ConfigureAwait(false);
        }

        return await RunProcessWithTokenAsync(fileName, arguments, onOutputLine, onErrorLine, cancellationToken, timeoutMs, onProgressLine).ConfigureAwait(false);
    }

    private async Task<InteractiveProcessResult> RunProcessNormalAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine,
        Action<string>? onErrorLine,
        CancellationToken cancellationToken,
        int timeoutMs)
    {
        using var timeoutCts = timeoutMs > 0 ? new CancellationTokenSource(timeoutMs) : new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        // Pipe mode has no transient \r fragments: every line is permanent output, and blank ones carry nothing.
        Action<string> forwardOutput = line => { if (line.Length > 0) onOutputLine?.Invoke(line); };
        Action<string> forwardError = line => { if (line.Length > 0) onErrorLine?.Invoke(line); };

        var result = await _processExecutor.ExecuteWithStreamingAsync(fileName, arguments, forwardOutput, forwardError, linkedCts.Token).ConfigureAwait(false);
        return new InteractiveProcessResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private async Task<InteractiveProcessResult> RunProcessWithTokenAsync(
        string fileName,
        string arguments,
        Action<string>? onOutputLine,
        Action<string>? onErrorLine,
        CancellationToken cancellationToken,
        int timeoutMs,
        Action<string>? onProgressLine = null)
    {
        if (!CreateInheritablePipe(out HANDLE stdoutReadHandle, out HANDLE stdoutWriteHandle))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create stdout pipe");

        if (!CreateInheritablePipe(out HANDLE stderrReadHandle, out HANDLE stderrWriteHandle))
        {
            PInvoke.CloseHandle(stdoutReadHandle);
            PInvoke.CloseHandle(stdoutWriteHandle);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create stderr pipe");
        }

        // Create a pipe for stdin (process won't use it, but we need a handle)
        if (!CreateInheritablePipe(out HANDLE stdinReadHandle, out HANDLE stdinWriteHandle))
        {
            PInvoke.CloseHandle(stdoutReadHandle);
            PInvoke.CloseHandle(stdoutWriteHandle);
            PInvoke.CloseHandle(stderrReadHandle);
            PInvoke.CloseHandle(stderrWriteHandle);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create stdin pipe");
        }

        PInvoke.SetHandleInformation(stdoutReadHandle, (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT, 0);
        PInvoke.SetHandleInformation(stderrReadHandle, (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT, 0);
        PInvoke.SetHandleInformation(stdinWriteHandle, (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT, 0);

        try
        {
            IntPtr envBlock = CreateEnvironmentBlockFor(_interactiveUserToken);
            var processHandle = HANDLE.Null;

            try
            {
                var startupInfo = new STARTUPINFOW
                {
                    dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES,
                    hStdInput = stdinReadHandle,
                    hStdOutput = stdoutWriteHandle,
                    hStdError = stderrWriteHandle,
                };

                var commandLine = $"\"{fileName}\" {arguments}";
                var creationFlags = PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW | PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;

                if (!TryCreateProcessWithToken(
                    _interactiveUserToken,
                    commandLine,
                    creationFlags,
                    envBlock,
                    desktop: null,
                    startupInfo,
                    out PROCESS_INFORMATION pi))
                {
                    var error = Marshal.GetLastWin32Error();
                    _logService.Log(LogLevel.Warning,
                        $"CreateProcessWithTokenW failed (error {error}), falling back to normal process execution");
                    return await RunProcessNormalAsync(fileName, arguments, onOutputLine, onErrorLine, cancellationToken, timeoutMs).ConfigureAwait(false);
                }

                processHandle = pi.hProcess;
                // Close the thread handle immediately — we only need the process handle
                PInvoke.CloseHandle(pi.hThread);

                // Close the write ends of the pipes (child process has them now)
                PInvoke.CloseHandle(stdoutWriteHandle);
                stdoutWriteHandle = default;
                PInvoke.CloseHandle(stderrWriteHandle);
                stderrWriteHandle = default;
                PInvoke.CloseHandle(stdinReadHandle);
                stdinReadHandle = default;
                PInvoke.CloseHandle(stdinWriteHandle);
                stdinWriteHandle = default;

                _logService.Log(LogLevel.Debug,
                    $"Launched process as interactive user '{_interactiveUserName}' (PID {pi.dwProcessId})");

                var stdoutBuilder = new StringBuilder();
                var stderrBuilder = new StringBuilder();

                var stdoutSafeHandle = new SafeFileHandle(stdoutReadHandle, ownsHandle: true);
                stdoutReadHandle = default; // SafeFileHandle now owns it
                var stderrSafeHandle = new SafeFileHandle(stderrReadHandle, ownsHandle: true);
                stderrReadHandle = default; // SafeFileHandle now owns it

                // timeoutMs 0 = no wall-clock limit (WinGetCliRunner's contract). CancellationTokenSource(0) is born
                // cancelled and would fire the kill below before the child has run.
                using var timeoutCts = timeoutMs > 0 ? new CancellationTokenSource(timeoutMs) : new CancellationTokenSource();
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                using var killRegistration = linkedCts.Token.Register(() =>
                {
                    try
                    {
                        using var proc = Process.GetProcessById((int)pi.dwProcessId);
                        proc.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex) { _logService.LogDebug($"Best-effort process kill on cancellation/timeout: {ex.Message}"); }
                });

                var readStdout = Task.Run(async () =>
                {
                    using var stream = new FileStream(stdoutSafeHandle, FileAccess.Read, bufferSize: 4096, isAsync: false);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    await WinGetCliRunner.ReadStdoutCharByCharAsync(
                        reader, stdoutBuilder, onOutputLine, onProgressLine).ConfigureAwait(false);
                }, CancellationToken.None);

                var readStderr = Task.Run(async () =>
                {
                    using var stream = new FileStream(stderrSafeHandle, FileAccess.Read, bufferSize: 4096, isAsync: false);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                    {
                        stderrBuilder.AppendLine(line);
                        onErrorLine?.Invoke(line);
                    }
                }, CancellationToken.None);

                await Task.WhenAll(readStdout, readStderr).ConfigureAwait(false);

                var waitMs = timeoutMs > 0 ? (uint)timeoutMs : PInvoke.INFINITE;
                var waitResult = await Task.Run(() => PInvoke.WaitForSingleObject(processHandle, waitMs)).ConfigureAwait(false);
                if (waitResult != WAIT_EVENT.WAIT_OBJECT_0)
                    _logService.Log(LogLevel.Warning, $"Process did not signal exit (wait result 0x{(uint)waitResult:X}); the exit code read below cannot be trusted");

                if (!TryGetExitCode(processHandle, out uint exitCode))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "GetExitCodeProcess failed; the process's exit code is unknown");

                return new InteractiveProcessResult(
                    (int)exitCode,
                    stdoutBuilder.ToString(),
                    stderrBuilder.ToString());
            }
            finally
            {
                if (!processHandle.IsNull)
                    PInvoke.CloseHandle(processHandle);
                if (envBlock != IntPtr.Zero)
                    FreeEnvironmentBlock(envBlock);
            }
        }
        finally
        {
            if (!stdoutReadHandle.IsNull) PInvoke.CloseHandle(stdoutReadHandle);
            if (!stdoutWriteHandle.IsNull) PInvoke.CloseHandle(stdoutWriteHandle);
            if (!stderrReadHandle.IsNull) PInvoke.CloseHandle(stderrReadHandle);
            if (!stderrWriteHandle.IsNull) PInvoke.CloseHandle(stderrWriteHandle);
            if (!stdinReadHandle.IsNull) PInvoke.CloseHandle(stdinReadHandle);
            if (!stdinWriteHandle.IsNull) PInvoke.CloseHandle(stdinWriteHandle);
        }
    }

    // CreateProcessWithTokenW without pipe redirection, so the child can create its own window on the interactive user's desktop.
    public void LaunchProcessAsInteractiveUser(string fileName, string arguments = "")
    {
        if (!_isOtsElevation || _interactiveUserToken.IsNull)
        {
            _ = _processExecutor.ShellExecuteAsync(fileName, arguments);
            return;
        }

        IntPtr envBlock = IntPtr.Zero;
        try
        {
            envBlock = CreateEnvironmentBlockFor(_interactiveUserToken);

            var commandLine = string.IsNullOrEmpty(arguments)
                ? $"\"{fileName}\""
                : $"\"{fileName}\" {arguments}";

            if (!TryCreateProcessWithToken(
                _interactiveUserToken,
                commandLine,
                PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT,
                envBlock,
                "winsta0\\default",
                default,
                out PROCESS_INFORMATION pi))
            {
                var error = Marshal.GetLastWin32Error();
                _logService.Log(LogLevel.Warning,
                    $"LaunchProcessAsInteractiveUser: CreateProcessWithTokenW failed (error {error}), falling back to ShellExecuteAsync");
                _ = _processExecutor.ShellExecuteAsync(fileName, arguments);
                return;
            }

            _logService.Log(LogLevel.Debug,
                $"Launched GUI process as interactive user '{_interactiveUserName}' (PID {pi.dwProcessId}): {fileName}");

            // Close both handles — we don't need to wait for the process
            PInvoke.CloseHandle(pi.hThread);
            PInvoke.CloseHandle(pi.hProcess);
        }
        finally
        {
            if (envBlock != IntPtr.Zero)
                FreeEnvironmentBlock(envBlock);
        }
    }

    // Also duplicates the token for later process creation.
    private string? TryGetSidFromExplorerToken()
    {
        try
        {
            uint consoleSessionId = PInvoke.WTSGetActiveConsoleSessionId();
            if (consoleSessionId == 0xFFFFFFFF)
                return null;

            var explorerProcesses = Process.GetProcessesByName("explorer");
            foreach (var proc in explorerProcesses)
            {
                try
                {
                    if (!PInvoke.ProcessIdToSessionId((uint)proc.Id, out uint procSessionId))
                        continue;

                    if (procSessionId != consoleSessionId)
                        continue;

                    if (!TryOpenProcessToken(proc, out HANDLE tokenHandle))
                        continue;

                    try
                    {
                        string? sid = ReadTokenUserSid(tokenHandle);
                        if (sid == null)
                            continue;

                        _logService.Log(LogLevel.Debug,
                            $"OTS detection: explorer.exe (PID {proc.Id}, session {consoleSessionId}) SID: {sid}");

                        // Duplicate the token as a primary token for CreateProcessWithTokenW
                        if (TryDuplicatePrimaryToken(tokenHandle, out HANDLE duplicatedToken))
                        {
                            _interactiveUserToken = duplicatedToken;
                            _logService.Log(LogLevel.Debug,
                                "OTS detection: Successfully duplicated interactive user token for process creation");
                        }
                        else
                        {
                            _logService.Log(LogLevel.Warning,
                                $"OTS detection: Failed to duplicate token (error {Marshal.GetLastWin32Error()})");
                        }

                        return sid;
                    }
                    finally
                    {
                        PInvoke.CloseHandle(tokenHandle);
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Debug,
                        $"OTS detection: Failed to read explorer.exe PID {proc.Id}: {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Debug, $"OTS detection: Explorer token approach failed: {ex.Message}");
        }

        return null;
    }

    private string? TryGetSidFromWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                using (obj)
                {
                    var domainUser = obj["UserName"]?.ToString();
                    if (string.IsNullOrEmpty(domainUser))
                        continue;

                    try
                    {
                        var ntAccount = new NTAccount(domainUser);
                        var sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
                        _logService.Log(LogLevel.Debug,
                            $"OTS detection: WMI returned user '{domainUser}' → SID: {sid.Value}");
                        return sid.Value;
                    }
                    catch (Exception ex)
                    {
                        _logService.Log(LogLevel.Debug,
                            $"OTS detection: Failed to translate WMI user '{domainUser}' to SID: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Debug, $"OTS detection: WMI approach failed: {ex.Message}");
        }

        return null;
    }

    private string? TryGetSidFromWtsSession()
    {
        try
        {
            uint consoleSessionId = PInvoke.WTSGetActiveConsoleSessionId();
            if (consoleSessionId == 0xFFFFFFFF)
                return null;

            string? username = QueryWtsSessionString(consoleSessionId, WTS_INFO_CLASS.WTSUserName);
            string? domain = QueryWtsSessionString(consoleSessionId, WTS_INFO_CLASS.WTSDomainName);

            if (string.IsNullOrEmpty(username))
                return null;

            string fullName = !string.IsNullOrEmpty(domain) ? $"{domain}\\{username}" : username;

            try
            {
                var ntAccount = new NTAccount(fullName);
                var sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
                _logService.Log(LogLevel.Debug,
                    $"OTS detection: WTS session returned user '{fullName}' → SID: {sid.Value}");
                return sid.Value;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Debug,
                    $"OTS detection: Failed to translate WTS user '{fullName}' to SID: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Debug, $"OTS detection: WTS session approach failed: {ex.Message}");
        }

        return null;
    }

    private static unsafe string? QueryWtsSessionString(uint sessionId, WTS_INFO_CLASS infoClass)
    {
        if (!PInvoke.WTSQuerySessionInformation(HANDLE.Null, sessionId, infoClass,
            out PWSTR buffer, out uint bytesReturned))
            return null;

        try
        {
            return bytesReturned > 0 ? buffer.ToString() : null;
        }
        finally
        {
            PInvoke.WTSFreeMemory(buffer.Value);
        }
    }

    private static unsafe bool CreateInheritablePipe(out HANDLE readHandle, out HANDLE writeHandle)
    {
        var sa = new SECURITY_ATTRIBUTES
        {
            nLength = (uint)sizeof(SECURITY_ATTRIBUTES),
            bInheritHandle = true,
            lpSecurityDescriptor = null
        };

        HANDLE read, write;
        bool created = PInvoke.CreatePipe(&read, &write, &sa, 0);
        readHandle = read;
        writeHandle = write;
        return created;
    }

    private static unsafe bool TryOpenProcessToken(Process process, out HANDLE tokenHandle)
    {
        HANDLE token;
        bool opened = PInvoke.OpenProcessToken(
            (HANDLE)process.Handle,
            TOKEN_ACCESS_MASK.TOKEN_QUERY | TOKEN_ACCESS_MASK.TOKEN_DUPLICATE,
            &token);
        tokenHandle = token;
        return opened;
    }

    private static unsafe bool TryDuplicatePrimaryToken(HANDLE token, out HANDLE primaryToken)
    {
        HANDLE duplicated;
        bool duplicatedOk = PInvoke.DuplicateTokenEx(
            token,
            TOKEN_ACCESS_MASK.TOKEN_ALL_ACCESS,
            null,
            SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
            TOKEN_TYPE.TokenPrimary,
            &duplicated);
        primaryToken = duplicated;
        return duplicatedOk;
    }

    // TOKEN_USER is variable length - the SID bytes trail the struct in the same buffer - so the size comes
    // from a zero-length probe call and the SID must be copied out before the buffer is freed.
    private static unsafe string? ReadTokenUserSid(HANDLE token)
    {
        uint bufferLength;
        PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser, null, 0, &bufferLength);

        IntPtr buffer = Marshal.AllocHGlobal((int)bufferLength);
        try
        {
            uint returnLength;
            if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser,
                (void*)buffer, bufferLength, &returnLength))
                return null;

            return new SecurityIdentifier(((TOKEN_USER*)buffer)->User.Sid).Value;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static unsafe IntPtr CreateEnvironmentBlockFor(HANDLE token)
    {
        void* environmentBlock = null;
        PInvoke.CreateEnvironmentBlock(&environmentBlock, token, false);
        return (IntPtr)environmentBlock;
    }

    private static unsafe void FreeEnvironmentBlock(IntPtr environmentBlock) =>
        PInvoke.DestroyEnvironmentBlock((void*)environmentBlock);

    private static unsafe bool TryGetExitCode(HANDLE process, out uint exitCode)
    {
        uint code;
        bool read = PInvoke.GetExitCodeProcess(process, &code);
        exitCode = code;
        return read;
    }

    // CreateProcessWithTokenW writes into lpCommandLine, so it gets a writable null-terminated buffer rather
    // than a string, and lpDesktop stays pinned for the whole call. Kept out of the async caller because
    // taking the address of a local there is CS9123 - the compiler may hoist it onto the GC heap.
    private static unsafe bool TryCreateProcessWithToken(
        HANDLE token,
        string commandLine,
        PROCESS_CREATION_FLAGS creationFlags,
        IntPtr environmentBlock,
        string? desktop,
        STARTUPINFOW startupInfo,
        out PROCESS_INFORMATION processInformation)
    {
        startupInfo.cb = (uint)sizeof(STARTUPINFOW);
        char[] commandLineBuffer = (commandLine + '\0').ToCharArray();

        fixed (char* pCommandLine = commandLineBuffer)
        fixed (char* pDesktop = desktop)
        {
            startupInfo.lpDesktop = pDesktop;

            PROCESS_INFORMATION pi;
            bool created = PInvoke.CreateProcessWithToken(
                token,
                CREATE_PROCESS_LOGON_FLAGS.LOGON_WITH_PROFILE,
                default,
                pCommandLine,
                creationFlags,
                (void*)environmentBlock,
                default,
                &startupInfo,
                &pi);
            processInformation = pi;
            return created;
        }
    }

    private string ResolveSidToUsername(string sidString)
    {
        try
        {
            var sid = new SecurityIdentifier(sidString);
            var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));
            var fullName = ntAccount.Value;
            var backslashIndex = fullName.IndexOf('\\');
            return backslashIndex >= 0 ? fullName[(backslashIndex + 1)..] : fullName;
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"Failed to resolve SID '{sidString}' to username: {ex.Message}");
            return Environment.UserName;
        }
    }

    private string ResolveProfilePath(string sidString)
    {
        try
        {
            using var profileKey = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sidString}");
            var profileImagePath = profileKey?.GetValue("ProfileImagePath") as string;
            if (!string.IsNullOrEmpty(profileImagePath))
            {
                return profileImagePath;
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Warning,
                $"Failed to resolve profile path for SID '{sidString}': {ex.Message}");
        }

        var username = ResolveSidToUsername(sidString);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86).Substring(0, 3),
            "Users", username);
    }

    public IShellRelaunchToken? CaptureShellRelaunchToken()
    {
        // Deliberately NOT gated on _isOtsElevation. When Marco runs Winhance elevated as himself
        // there is no OTS, so LaunchProcessAsInteractiveUser would fall through to Process.Start -
        // and an elevated Process.Start cannot bring the shell back. Harvest a token from the live
        // explorer.exe instead, while it still exists.
        try
        {
            uint consoleSessionId = PInvoke.WTSGetActiveConsoleSessionId();
            if (consoleSessionId == 0xFFFFFFFF)
            {
                _logService.Log(LogLevel.Warning, "[InteractiveUserService] No active console session - cannot capture a shell relaunch token");
                return null;
            }

            foreach (var proc in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    if (!PInvoke.ProcessIdToSessionId((uint)proc.Id, out uint procSessionId)
                        || procSessionId != consoleSessionId)
                        continue;

                    if (!TryOpenProcessToken(proc, out HANDLE tokenHandle))
                        continue;

                    try
                    {
                        if (TryDuplicatePrimaryToken(tokenHandle, out HANDLE duplicatedToken))
                        {
                            _logService.Log(LogLevel.Debug,
                                $"[InteractiveUserService] Captured a shell relaunch token from explorer.exe (PID {proc.Id})");
                            return new ShellRelaunchToken(duplicatedToken, _logService);
                        }

                        _logService.Log(LogLevel.Warning,
                            $"[InteractiveUserService] Failed to duplicate the shell token (error {Marshal.GetLastWin32Error()})");
                    }
                    finally
                    {
                        PInvoke.CloseHandle(tokenHandle);
                    }
                }
                catch (Exception ex)
                {
                    _logService.Log(LogLevel.Debug,
                        $"[InteractiveUserService] Could not read explorer.exe PID {proc.Id}: {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }

            _logService.Log(LogLevel.Warning, "[InteractiveUserService] No usable explorer.exe found to capture a shell relaunch token from");
            return null;
        }
        catch (Exception ex)
        {
            _logService.LogError("Failed to capture a shell relaunch token", ex);
            return null;
        }
    }

    private sealed class ShellRelaunchToken(HANDLE token, ILogService logService) : IShellRelaunchToken
    {
        private HANDLE _token = token;

        public bool TryLaunch(string fileName, string arguments = "")
        {
            if (_token.IsNull)
                return false;

            IntPtr envBlock = IntPtr.Zero;
            try
            {
                envBlock = CreateEnvironmentBlockFor(_token);

                var commandLine = string.IsNullOrEmpty(arguments)
                    ? $"\"{fileName}\""
                    : $"\"{fileName}\" {arguments}";

                if (!TryCreateProcessWithToken(
                    _token,
                    commandLine,
                    PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT,
                    envBlock,
                    "winsta0\\default",
                    default,
                    out PROCESS_INFORMATION pi))
                {
                    logService.Log(LogLevel.Warning,
                        $"[ShellRelaunchToken] CreateProcessWithTokenW failed for '{fileName}' (error {Marshal.GetLastWin32Error()})");
                    return false;
                }

                logService.Log(LogLevel.Info,
                    $"[ShellRelaunchToken] Relaunched '{fileName}' as the interactive shell user (PID {pi.dwProcessId})");
                PInvoke.CloseHandle(pi.hThread);
                PInvoke.CloseHandle(pi.hProcess);
                return true;
            }
            catch (Exception ex)
            {
                logService.LogError($"[ShellRelaunchToken] Failed to relaunch '{fileName}'", ex);
                return false;
            }
            finally
            {
                if (envBlock != IntPtr.Zero)
                    FreeEnvironmentBlock(envBlock);
            }
        }

        public void Dispose()
        {
            if (!_token.IsNull)
            {
                PInvoke.CloseHandle(_token);
                _token = default;
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (!_interactiveUserToken.IsNull)
            {
                PInvoke.CloseHandle(_interactiveUserToken);
                _interactiveUserToken = default;
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
