using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

// So the child sees isatty(stdout)==true and emits real-time progress bars. ConPTY needs Windows 10 build
// 17763 (1809); below that CreatePseudoConsole is absent from kernel32 and every call here throws
// EntryPointNotFoundException.
internal sealed class ConPtyProcess : IDisposable
{
    private HPCON _hPC;
    private HANDLE _pipeWeWriteToConsole;
    private HANDLE _pipeWeReadFromConsole;
    private HANDLE _hProcess;
    private HANDLE _hThread;
    private LPPROC_THREAD_ATTRIBUTE_LIST _attrList;
    private bool _disposed;

    public int ExitCode { get; private set; } = -1;

    // Lines are classified by VT100 cursor-to-column-1 / \r (progress) vs \n (permanent). Cancellation policy is
    // the caller's - pass a composed token (wall-clock + idle + caller cancel).
    public async Task<WinGetCliRunner.WinGetCliResult> RunAsync(
        string exePath,
        string arguments,
        Action<string>? onOutputLine,
        Action<string>? onProgressLine,
        CancellationToken cancellationToken)
    {
        uint processId = StartChild(exePath, arguments);

        var stdoutBuilder = new StringBuilder();

        using var killReg = cancellationToken.Register(() =>
        {
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)processId);
                proc.Kill(entireProcessTree: true);
            }
            catch { }
        });

        var readTask = Task.Run(() =>
        {
            ReadConPtyOutput(
                _pipeWeReadFromConsole, stdoutBuilder,
                onOutputLine, onProgressLine);
        }, CancellationToken.None);

        // Wait on a thread pool thread to avoid blocking the UI thread.
        await Task.Run(() => PInvoke.WaitForSingleObject(_hProcess, PInvoke.INFINITE)).ConfigureAwait(false);
        ExitCode = (int)GetExitCode(_hProcess);

        // Close pseudo console so the output pipe sees EOF.
        if (!_hPC.IsNull)
        {
            PInvoke.ClosePseudoConsole(_hPC);
            _hPC = default;
        }

        await readTask.ConfigureAwait(false);

        return new WinGetCliRunner.WinGetCliResult(
            ExitCode,
            stdoutBuilder.ToString(),
            string.Empty); // ConPTY merges stderr into stdout
    }

    // Kept out of RunAsync because taking the address of a local inside an async method is CS9123: the
    // compiler may hoist that local into the state machine on the heap, where the GC can move it out from
    // under the pointer.
    private unsafe uint StartChild(string exePath, string arguments)
    {
        var sa = new SECURITY_ATTRIBUTES
        {
            nLength = (uint)sizeof(SECURITY_ATTRIBUTES),
            bInheritHandle = true
        };

        HANDLE inputReadSide = default;
        HANDLE outputWriteSide = default;

        try
        {
            HANDLE writeToConsole;
            if (!PInvoke.CreatePipe(&inputReadSide, &writeToConsole, &sa, 0))
                throw new InvalidOperationException($"CreatePipe(input) failed: {Marshal.GetLastWin32Error()}");
            _pipeWeWriteToConsole = writeToConsole;

            HANDLE readFromConsole;
            if (!PInvoke.CreatePipe(&readFromConsole, &outputWriteSide, &sa, 0))
                throw new InvalidOperationException($"CreatePipe(output) failed: {Marshal.GetLastWin32Error()}");
            _pipeWeReadFromConsole = readFromConsole;

            var size = new COORD { X = 120, Y = 30 };
            HPCON hPC;
            HRESULT hr = PInvoke.CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, &hPC);
            _hPC = hPC;
            if (hr != 0)
                throw new InvalidOperationException($"CreatePseudoConsole failed: HRESULT 0x{(int)hr:X8}");
        }
        catch
        {
            if (!inputReadSide.IsNull) PInvoke.CloseHandle(inputReadSide);
            if (!outputWriteSide.IsNull) PInvoke.CloseHandle(outputWriteSide);
            throw;
        }

        PInvoke.CloseHandle(inputReadSide);
        PInvoke.CloseHandle(outputWriteSide);

        nuint attrSize = 0;
        PInvoke.InitializeProcThreadAttributeList(default, 1, ref attrSize);
        var attrList = (LPPROC_THREAD_ATTRIBUTE_LIST)Marshal.AllocHGlobal((nint)attrSize);

        // The field is assigned only once init has succeeded. Dispose runs DeleteProcThreadAttributeList over
        // whatever the field holds, and running that over uninitialised heap is an access violation that takes
        // the process down rather than an exception a caller could catch.
        if (!PInvoke.InitializeProcThreadAttributeList(attrList, 1, ref attrSize))
        {
            var error = Marshal.GetLastWin32Error();
            Marshal.FreeHGlobal(attrList);
            throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {error}");
        }

        _attrList = attrList;

        // PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE is the exception to lpValue-is-a-pointer: the HPCON goes in BY
        // VALUE, as Microsoft's own ConPTY samples do it. Passing its address instead compiles, returns
        // success, and silently starts a child with no pseudoconsole.
        if (!PInvoke.UpdateProcThreadAttribute(
                _attrList, 0,
                PInvoke.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                (void*)(nint)_hPC,
                (nuint)sizeof(HPCON),
                null, null))
            throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

        var si = default(STARTUPINFOEXW);
        si.StartupInfo.cb = (uint)sizeof(STARTUPINFOEXW);
        si.lpAttributeList = _attrList;

        // CreateProcess is documented to modify lpCommandLine in place, so it gets its own writable,
        // null-terminated buffer rather than a string.
        char[] commandLine = $"\"{exePath}\" {arguments}\0".ToCharArray();

        PROCESS_INFORMATION pi;
        fixed (char* pCommandLine = commandLine)
        {
            if (!PInvoke.CreateProcess(
                    default, pCommandLine,
                    null, null,
                    false, PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT,
                    null, default,
                    (STARTUPINFOW*)&si, &pi))
                throw new InvalidOperationException($"CreateProcessW failed: {Marshal.GetLastWin32Error()}");
        }

        _hProcess = pi.hProcess;
        _hThread = pi.hThread;
        return pi.dwProcessId;
    }

    private static unsafe uint GetExitCode(HANDLE process)
    {
        uint exitCode = 0;
        PInvoke.GetExitCodeProcess(process, &exitCode);
        return exitCode;
    }

    private enum VtState { Normal, EscSeen, Csi, Osc }

    // Safety limits — abandon malformed sequences that exceed these lengths
    private const int MaxCsiLen = 128;
    private const int MaxOscLen = 1024;

    // \r and \x1b[G / \x1b[1G are progress-line indicators. Progress lines get their unfilled bar track filled in -
    // winget draws it with cursor positioning + background colours, invisible without terminal colour support.
    private static void ReadConPtyOutput(
        HANDLE pipeHandle,
        StringBuilder outputBuilder,
        Action<string>? onOutputLine,
        Action<string>? onProgressLine)
    {
        using var stream = new FileStream(
            new Microsoft.Win32.SafeHandles.SafeFileHandle(pipeHandle, ownsHandle: false),
            FileAccess.Read);

        var currentLine = new StringBuilder();
        string? lastStringBeforeLF = null;
        var buffer = new byte[4096];
        var charBuf = new char[4096];
        var decoder = Encoding.UTF8.GetDecoder();

        var vtState = VtState.Normal;
        var csiBuf = new StringBuilder();
        int oscLen = 0;

        while (true)
        {
            int bytesRead;
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
            }
            catch (IOException)
            {
                break;
            }

            if (bytesRead == 0)
                break;

            int charCount = decoder.GetChars(buffer, 0, bytesRead, charBuf, 0);

            for (int i = 0; i < charCount; i++)
            {
                char c = charBuf[i];

                switch (vtState)
                {
                    case VtState.EscSeen:
                        if (c == '[')
                        {
                            vtState = VtState.Csi;
                            csiBuf.Clear();
                        }
                        else if (c == ']')
                        {
                            vtState = VtState.Osc;
                            oscLen = 0;
                        }
                        else
                        {
                            vtState = VtState.Normal;
                        }
                        continue;

                    case VtState.Csi:
                        csiBuf.Append(c);
                        if (c >= '@' && c <= '~')
                        {
                            var param = csiBuf.ToString();
                            vtState = VtState.Normal;

                            if (param == "G" || param == "1G")
                            {
                                EmitProgressLine(
                                    currentLine, ref lastStringBeforeLF,
                                    outputBuilder, onProgressLine);
                            }
                        }
                        else if (csiBuf.Length > MaxCsiLen)
                        {
                            vtState = VtState.Normal;
                        }
                        continue;

                    case VtState.Osc:
                        oscLen++;
                        if (c == '\x07')
                        {
                            vtState = VtState.Normal;
                        }
                        else if (c == '\x1b')
                        {
                            vtState = VtState.EscSeen;
                        }
                        else if (oscLen > MaxOscLen)
                        {
                            vtState = VtState.Normal;
                        }
                        continue;

                    default:
                        break;
                }

                if (c == '\x1b')
                {
                    vtState = VtState.EscSeen;
                }
                else if (c == '\n')
                {
                    if (currentLine.Length == 0)
                    {
                        if (lastStringBeforeLF is not null)
                        {
                            onOutputLine?.Invoke(lastStringBeforeLF);
                            lastStringBeforeLF = null;
                        }
                        continue;
                    }
                    string line = currentLine.ToString();
                    outputBuilder.AppendLine(line);
                    onOutputLine?.Invoke(line);
                    currentLine.Clear();
                    lastStringBeforeLF = null;
                }
                else if (c == '\r')
                {
                    EmitProgressLine(
                        currentLine, ref lastStringBeforeLF,
                        outputBuilder, onProgressLine);
                }
                else if (c >= ' ')
                {
                    currentLine.Append(c);
                }
            }
        }

        if (currentLine.Length > 0)
        {
            string line = currentLine.ToString();
            outputBuilder.AppendLine(line);
            onOutputLine?.Invoke(line);
        }
    }

    private static void EmitProgressLine(
        StringBuilder currentLine,
        ref string? lastStringBeforeLF,
        StringBuilder outputBuilder,
        Action<string>? onProgressLine)
    {
        if (currentLine.Length == 0) return;
        string line = FillProgressBarTrack(currentLine.ToString());
        lastStringBeforeLF = line;
        outputBuilder.AppendLine(line);
        onProgressLine?.Invoke(line);
        currentLine.Clear();
    }

    // Winget's VT bar is 30 cells: filled cells use U+2588 (partials U+2589-U+258F); unfilled cells are drawn via
    // background colour or cursor positioning, invisible after VT stripping - so U+2591 is inserted for the track.
    private static string FillProgressBarTrack(string line)
    {
        int barStart = -1;
        int barEnd = -1;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] >= '\u2588' && line[i] <= '\u258F')
            {
                if (barStart < 0) barStart = i;
                barEnd = i;
            }
            else if (barStart >= 0)
            {
                break;
            }
        }

        if (barStart < 0)
            return line;

        int filledCount = barEnd - barStart + 1;

        const int BarWidth = 30;
        int unfilledCount = BarWidth - filledCount;

        if (unfilledCount <= 0)
            return line;

        // Find where the text content starts after the bar area.
        // Skip any trailing spaces (these are the invisible unfilled area
        // or separator whitespace from the stripped VT output).
        int afterBar = barEnd + 1;
        while (afterBar < line.Length && line[afterBar] == ' ')
            afterBar++;

        // Rebuild: [prefix before bar][filled blocks][░ unfilled track][  text]
        var sb = new StringBuilder(line.Length + unfilledCount);
        sb.Append(line, 0, barEnd + 1);
        sb.Append('\u2591', unfilledCount);
        if (afterBar < line.Length)
        {
            sb.Append("  ");
            sb.Append(line, afterBar, line.Length - afterBar);
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!_hPC.IsNull)
        {
            PInvoke.ClosePseudoConsole(_hPC);
            _hPC = default;
        }

        if (!_attrList.IsNull)
        {
            PInvoke.DeleteProcThreadAttributeList(_attrList);
            Marshal.FreeHGlobal(_attrList);
            _attrList = default;
        }

        if (!_hThread.IsNull) { PInvoke.CloseHandle(_hThread); _hThread = default; }
        if (!_hProcess.IsNull) { PInvoke.CloseHandle(_hProcess); _hProcess = default; }
        if (!_pipeWeWriteToConsole.IsNull) { PInvoke.CloseHandle(_pipeWeWriteToConsole); _pipeWeWriteToConsole = default; }
        if (!_pipeWeReadFromConsole.IsNull) { PInvoke.CloseHandle(_pipeWeReadFromConsole); _pipeWeReadFromConsole = default; }
    }
}
