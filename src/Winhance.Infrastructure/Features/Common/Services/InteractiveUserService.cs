using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;

namespace Winhance.Infrastructure.Features.Common.Services
{
    [SupportedOSPlatform("windows")]
    public class InteractiveUserService : IInteractiveUserService, IDisposable
    {
        private readonly ILogService _logService;

        private readonly bool _isOtsElevation;
        private readonly string? _interactiveUserSid;
        private readonly string _interactiveUserName;
        private readonly string _interactiveUserProfilePath;
        private IntPtr _interactiveUserToken = IntPtr.Zero;
        private bool _disposed;

        public bool IsOtsElevation => _isOtsElevation;
        public string? InteractiveUserSid => _interactiveUserSid;
        public string InteractiveUserName => _interactiveUserName;
        public string InteractiveUserProfilePath => _interactiveUserProfilePath;
        public bool HasInteractiveUserToken => _interactiveUserToken != IntPtr.Zero;

        public InteractiveUserService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Detect OTS elevation
            var currentSid = WindowsIdentity.GetCurrent().User?.Value;
            string? detectedSid = null;

            // Fallback chain
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

                if (_interactiveUserToken != IntPtr.Zero)
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

        /// <summary>
        /// Runs a process as the interactive user using the stored token.
        /// Falls back to normal Process.Start if no token is available.
        /// </summary>
        public async Task<InteractiveProcessResult> RunProcessAsInteractiveUserAsync(
            string fileName,
            string arguments,
            Action<string>? onOutputLine = null,
            Action<string>? onErrorLine = null,
            CancellationToken cancellationToken = default,
            int timeoutMs = 300_000)
        {
            if (!_isOtsElevation || _interactiveUserToken == IntPtr.Zero)
            {
                // No OTS or no token — fall back to normal process execution
                return await RunProcessNormalAsync(fileName, arguments, onOutputLine, onErrorLine, cancellationToken, timeoutMs);
            }

            return await RunProcessWithTokenAsync(fileName, arguments, onOutputLine, onErrorLine, cancellationToken, timeoutMs);
        }

        /// <summary>
        /// Normal process execution (non-OTS fallback).
        /// </summary>
        private async Task<InteractiveProcessResult> RunProcessNormalAsync(
            string fileName,
            string arguments,
            Action<string>? onOutputLine,
            Action<string>? onErrorLine,
            CancellationToken cancellationToken,
            int timeoutMs)
        {
            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            process.Start();

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            using var registration = linkedCts.Token.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            });

            var readStdout = Task.Run(async () =>
            {
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    stdoutBuilder.AppendLine(line);
                    onOutputLine?.Invoke(line);
                }
            }, CancellationToken.None);

            var readStderr = Task.Run(async () =>
            {
                while (await process.StandardError.ReadLineAsync() is { } line)
                {
                    stderrBuilder.AppendLine(line);
                    onErrorLine?.Invoke(line);
                }
            }, CancellationToken.None);

            await Task.WhenAll(readStdout, readStderr);
            await process.WaitForExitAsync(linkedCts.Token);

            return new InteractiveProcessResult(
                process.ExitCode,
                stdoutBuilder.ToString(),
                stderrBuilder.ToString());
        }

        /// <summary>
        /// Runs a process as the interactive user using CreateProcessWithTokenW.
        /// </summary>
        private async Task<InteractiveProcessResult> RunProcessWithTokenAsync(
            string fileName,
            string arguments,
            Action<string>? onOutputLine,
            Action<string>? onErrorLine,
            CancellationToken cancellationToken,
            int timeoutMs)
        {
            // Create pipes for stdout and stderr
            var sa = new UserTokenApi.SECURITY_ATTRIBUTES
            {
                nLength = Marshal.SizeOf<UserTokenApi.SECURITY_ATTRIBUTES>(),
                bInheritHandle = true,
                lpSecurityDescriptor = IntPtr.Zero
            };

            if (!UserTokenApi.CreatePipe(out IntPtr stdoutReadHandle, out IntPtr stdoutWriteHandle, ref sa, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create stdout pipe");

            if (!UserTokenApi.CreatePipe(out IntPtr stderrReadHandle, out IntPtr stderrWriteHandle, ref sa, 0))
            {
                UserTokenApi.CloseHandle(stdoutReadHandle);
                UserTokenApi.CloseHandle(stdoutWriteHandle);
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create stderr pipe");
            }

            // Create a pipe for stdin (process won't use it, but we need a handle)
            if (!UserTokenApi.CreatePipe(out IntPtr stdinReadHandle, out IntPtr stdinWriteHandle, ref sa, 0))
            {
                UserTokenApi.CloseHandle(stdoutReadHandle);
                UserTokenApi.CloseHandle(stdoutWriteHandle);
                UserTokenApi.CloseHandle(stderrReadHandle);
                UserTokenApi.CloseHandle(stderrWriteHandle);
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create stdin pipe");
            }

            // Ensure the read handles for stdout/stderr are NOT inherited by the child process
            UserTokenApi.SetHandleInformation(stdoutReadHandle, UserTokenApi.HANDLE_FLAG_INHERIT, 0);
            UserTokenApi.SetHandleInformation(stderrReadHandle, UserTokenApi.HANDLE_FLAG_INHERIT, 0);
            // Ensure the write handle for stdin is NOT inherited
            UserTokenApi.SetHandleInformation(stdinWriteHandle, UserTokenApi.HANDLE_FLAG_INHERIT, 0);

            try
            {
                // Create environment block for the interactive user
                IntPtr envBlock = IntPtr.Zero;
                UserTokenApi.CreateEnvironmentBlock(out envBlock, _interactiveUserToken, false);

                try
                {
                    var si = new UserTokenApi.STARTUPINFO
                    {
                        cb = Marshal.SizeOf<UserTokenApi.STARTUPINFO>(),
                        dwFlags = UserTokenApi.STARTF_USESTDHANDLES,
                        hStdInput = stdinReadHandle,
                        hStdOutput = stdoutWriteHandle,
                        hStdError = stderrWriteHandle,
                    };

                    var commandLine = $"\"{fileName}\" {arguments}";
                    int creationFlags = UserTokenApi.CREATE_NO_WINDOW | UserTokenApi.CREATE_UNICODE_ENVIRONMENT;

                    if (!UserTokenApi.CreateProcessWithTokenW(
                        _interactiveUserToken,
                        UserTokenApi.LOGON_WITH_PROFILE,
                        null,
                        commandLine,
                        creationFlags,
                        envBlock,
                        null,
                        ref si,
                        out UserTokenApi.PROCESS_INFORMATION pi))
                    {
                        var error = Marshal.GetLastWin32Error();
                        _logService.Log(LogLevel.Warning,
                            $"CreateProcessWithTokenW failed (error {error}), falling back to normal process execution");
                        // Fall back to normal execution
                        return await RunProcessNormalAsync(fileName, arguments, onOutputLine, onErrorLine, cancellationToken, timeoutMs);
                    }

                    // Close the thread handle immediately — we only need the process handle
                    UserTokenApi.CloseHandle(pi.hThread);

                    // Close the write ends of the pipes (child process has them now)
                    UserTokenApi.CloseHandle(stdoutWriteHandle);
                    stdoutWriteHandle = IntPtr.Zero;
                    UserTokenApi.CloseHandle(stderrWriteHandle);
                    stderrWriteHandle = IntPtr.Zero;
                    UserTokenApi.CloseHandle(stdinReadHandle);
                    stdinReadHandle = IntPtr.Zero;
                    UserTokenApi.CloseHandle(stdinWriteHandle);
                    stdinWriteHandle = IntPtr.Zero;

                    _logService.Log(LogLevel.Debug,
                        $"Launched process as interactive user '{_interactiveUserName}' (PID {pi.dwProcessId})");

                    // Read stdout/stderr using SafeFileHandle → FileStream → StreamReader
                    var stdoutBuilder = new StringBuilder();
                    var stderrBuilder = new StringBuilder();

                    var stdoutSafeHandle = new SafeFileHandle(stdoutReadHandle, ownsHandle: true);
                    stdoutReadHandle = IntPtr.Zero; // SafeFileHandle now owns it
                    var stderrSafeHandle = new SafeFileHandle(stderrReadHandle, ownsHandle: true);
                    stderrReadHandle = IntPtr.Zero; // SafeFileHandle now owns it

                    var processHandle = pi.hProcess;

                    // Set up cancellation
                    using var timeoutCts = new CancellationTokenSource(timeoutMs);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    using var killRegistration = linkedCts.Token.Register(() =>
                    {
                        try
                        {
                            using var proc = Process.GetProcessById(pi.dwProcessId);
                            proc.Kill(entireProcessTree: true);
                        }
                        catch { }
                    });

                    var readStdout = Task.Run(async () =>
                    {
                        using var stream = new FileStream(stdoutSafeHandle, FileAccess.Read, bufferSize: 4096, isAsync: false);
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        while (await reader.ReadLineAsync() is { } line)
                        {
                            stdoutBuilder.AppendLine(line);
                            onOutputLine?.Invoke(line);
                        }
                    }, CancellationToken.None);

                    var readStderr = Task.Run(async () =>
                    {
                        using var stream = new FileStream(stderrSafeHandle, FileAccess.Read, bufferSize: 4096, isAsync: false);
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        while (await reader.ReadLineAsync() is { } line)
                        {
                            stderrBuilder.AppendLine(line);
                            onErrorLine?.Invoke(line);
                        }
                    }, CancellationToken.None);

                    await Task.WhenAll(readStdout, readStderr);

                    // Wait for process exit and get exit code
                    await Task.Run(() =>
                    {
                        UserTokenApi.WaitForSingleObject(processHandle, (uint)timeoutMs);
                    });

                    UserTokenApi.GetExitCodeProcess(processHandle, out uint exitCode);
                    UserTokenApi.CloseHandle(processHandle);

                    return new InteractiveProcessResult(
                        (int)exitCode,
                        stdoutBuilder.ToString(),
                        stderrBuilder.ToString());
                }
                finally
                {
                    if (envBlock != IntPtr.Zero)
                        UserTokenApi.DestroyEnvironmentBlock(envBlock);
                }
            }
            finally
            {
                // Clean up any handles that weren't transferred to SafeFileHandle
                if (stdoutReadHandle != IntPtr.Zero) UserTokenApi.CloseHandle(stdoutReadHandle);
                if (stdoutWriteHandle != IntPtr.Zero) UserTokenApi.CloseHandle(stdoutWriteHandle);
                if (stderrReadHandle != IntPtr.Zero) UserTokenApi.CloseHandle(stderrReadHandle);
                if (stderrWriteHandle != IntPtr.Zero) UserTokenApi.CloseHandle(stderrWriteHandle);
                if (stdinReadHandle != IntPtr.Zero) UserTokenApi.CloseHandle(stdinReadHandle);
                if (stdinWriteHandle != IntPtr.Zero) UserTokenApi.CloseHandle(stdinWriteHandle);
            }
        }

        /// <summary>
        /// Fallback 1: Find explorer.exe in the active console session and read its process token SID.
        /// Also duplicates the token for later use in process creation.
        /// </summary>
        private string? TryGetSidFromExplorerToken()
        {
            try
            {
                uint consoleSessionId = UserTokenApi.WTSGetActiveConsoleSessionId();
                if (consoleSessionId == 0xFFFFFFFF)
                    return null;

                var explorerProcesses = Process.GetProcessesByName("explorer");
                foreach (var proc in explorerProcesses)
                {
                    try
                    {
                        if (!UserTokenApi.ProcessIdToSessionId((uint)proc.Id, out uint procSessionId))
                            continue;

                        if (procSessionId != consoleSessionId)
                            continue;

                        if (!UserTokenApi.OpenProcessToken(proc.Handle,
                            UserTokenApi.TOKEN_QUERY | UserTokenApi.TOKEN_DUPLICATE,
                            out IntPtr tokenHandle))
                            continue;

                        try
                        {
                            // First call: get required buffer size
                            UserTokenApi.GetTokenInformation(tokenHandle,
                                UserTokenApi.TOKEN_INFORMATION_CLASS.TokenUser,
                                IntPtr.Zero, 0, out uint tokenInfoLength);

                            IntPtr tokenInfo = Marshal.AllocHGlobal((int)tokenInfoLength);
                            try
                            {
                                if (UserTokenApi.GetTokenInformation(tokenHandle,
                                    UserTokenApi.TOKEN_INFORMATION_CLASS.TokenUser,
                                    tokenInfo, tokenInfoLength, out _))
                                {
                                    var tokenUser = Marshal.PtrToStructure<UserTokenApi.TOKEN_USER>(tokenInfo);
                                    var sid = new SecurityIdentifier(tokenUser.User.Sid);
                                    _logService.Log(LogLevel.Debug,
                                        $"OTS detection: explorer.exe (PID {proc.Id}, session {consoleSessionId}) SID: {sid.Value}");

                                    // Duplicate the token as a primary token for CreateProcessWithTokenW
                                    if (UserTokenApi.DuplicateTokenEx(
                                        tokenHandle,
                                        UserTokenApi.TOKEN_ALL_ACCESS,
                                        IntPtr.Zero,
                                        UserTokenApi.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                                        UserTokenApi.TOKEN_TYPE.TokenPrimary,
                                        out IntPtr duplicatedToken))
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

                                    return sid.Value;
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(tokenInfo);
                            }
                        }
                        finally
                        {
                            UserTokenApi.CloseHandle(tokenHandle);
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

        /// <summary>
        /// Fallback 2: Query WMI Win32_ComputerSystem.UserName and translate to SID.
        /// </summary>
        private string? TryGetSidFromWmi()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT UserName FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in searcher.Get())
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
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Debug, $"OTS detection: WMI approach failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Fallback 3: Use WTS Session API to get the console session's username/domain and translate to SID.
        /// </summary>
        private string? TryGetSidFromWtsSession()
        {
            try
            {
                uint consoleSessionId = UserTokenApi.WTSGetActiveConsoleSessionId();
                if (consoleSessionId == 0xFFFFFFFF)
                    return null;

                string? username = QueryWtsSessionString(consoleSessionId, UserTokenApi.WTS_INFO_CLASS.WTSUserName);
                string? domain = QueryWtsSessionString(consoleSessionId, UserTokenApi.WTS_INFO_CLASS.WTSDomainName);

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

        private static string? QueryWtsSessionString(uint sessionId, UserTokenApi.WTS_INFO_CLASS infoClass)
        {
            if (!UserTokenApi.WTSQuerySessionInformationW(IntPtr.Zero, sessionId, infoClass,
                out IntPtr buffer, out uint bytesReturned))
                return null;

            try
            {
                return bytesReturned > 0 ? Marshal.PtrToStringUni(buffer) : null;
            }
            finally
            {
                UserTokenApi.WTSFreeMemory(buffer);
            }
        }

        private string ResolveSidToUsername(string sidString)
        {
            try
            {
                var sid = new SecurityIdentifier(sidString);
                var ntAccount = (NTAccount)sid.Translate(typeof(NTAccount));
                var fullName = ntAccount.Value;
                // Return just the username part (strip DOMAIN\)
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

            // Fallback: construct from C:\Users\{username}
            var username = ResolveSidToUsername(sidString);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.SystemX86).Substring(0, 3),
                "Users", username);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_interactiveUserToken != IntPtr.Zero)
                {
                    UserTokenApi.CloseHandle(_interactiveUserToken);
                    _interactiveUserToken = IntPtr.Zero;
                }
                _disposed = true;
            }
        }
    }
}
