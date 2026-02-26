using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Native;
using static Winhance.Core.Features.Common.Native.ConPtyApi;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;

/// <summary>
/// Runs a process under a Windows Pseudo Console (ConPTY) so that the child
/// process sees isatty(stdout)==true and outputs real-time progress bars.
/// </summary>
internal sealed class ConPtyProcess : IDisposable
{
    private IntPtr _hPC = IntPtr.Zero;
    private IntPtr _pipeWeWriteToConsole = IntPtr.Zero;
    private IntPtr _pipeWeReadFromConsole = IntPtr.Zero;
    private IntPtr _hProcess = IntPtr.Zero;
    private IntPtr _hThread = IntPtr.Zero;
    private IntPtr _attrList = IntPtr.Zero;
    private bool _disposed;

    public int ExitCode { get; private set; } = -1;

    /// <summary>
    /// Launches the given executable under a ConPTY and reads output,
    /// classifying lines by VT100 cursor-to-column-1 / \r (progress)
    /// vs \n (permanent).
    /// </summary>
    public async Task<WinGetCliRunner.WinGetCliResult> RunAsync(
        string exePath,
        string arguments,
        Action<string>? onOutputLine,
        Action<string>? onErrorLine,
        Action<string>? onProgressLine,
        CancellationToken cancellationToken,
        int timeoutMs)
    {
        var sa = new SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = true
        };

        IntPtr inputReadSide = IntPtr.Zero;
        IntPtr outputWriteSide = IntPtr.Zero;

        try
        {
            if (!CreatePipe(out inputReadSide, out _pipeWeWriteToConsole, ref sa, 0))
                throw new InvalidOperationException($"CreatePipe(input) failed: {Marshal.GetLastWin32Error()}");

            if (!CreatePipe(out _pipeWeReadFromConsole, out outputWriteSide, ref sa, 0))
                throw new InvalidOperationException($"CreatePipe(output) failed: {Marshal.GetLastWin32Error()}");

            var size = new COORD(120, 30);
            int hr = CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out _hPC);
            if (hr != 0)
                throw new InvalidOperationException($"CreatePseudoConsole failed: HRESULT 0x{hr:X8}");
        }
        catch
        {
            if (inputReadSide != IntPtr.Zero) CloseHandle(inputReadSide);
            if (outputWriteSide != IntPtr.Zero) CloseHandle(outputWriteSide);
            throw;
        }

        CloseHandle(inputReadSide);
        CloseHandle(outputWriteSide);

        var attrSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
        _attrList = Marshal.AllocHGlobal(attrSize);

        if (!InitializeProcThreadAttributeList(_attrList, 1, 0, ref attrSize))
            throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");

        if (!UpdateProcThreadAttribute(
                _attrList, 0,
                PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                _hPC,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero, IntPtr.Zero))
            throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

        var si = new STARTUPINFOEX();
        si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
        si.lpAttributeList = _attrList;

        var cmdLine = $"\"{exePath}\" {arguments}";

        if (!CreateProcessW(
                null, cmdLine,
                IntPtr.Zero, IntPtr.Zero,
                false, EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero, null,
                ref si, out var pi))
            throw new InvalidOperationException($"CreateProcessW failed: {Marshal.GetLastWin32Error()}");

        _hProcess = pi.hProcess;
        _hThread = pi.hThread;

        var stdoutBuilder = new StringBuilder();
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var killReg = linked.Token.Register(() =>
        {
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById(pi.dwProcessId);
                proc.Kill(entireProcessTree: true);
            }
            catch { /* Best-effort process kill — process may have already exited */ }
        });

        var readTask = Task.Run(() =>
        {
            ReadConPtyOutput(
                _pipeWeReadFromConsole, stdoutBuilder,
                onOutputLine, onProgressLine);
        }, CancellationToken.None);

        // Wait on a thread pool thread to avoid blocking the UI thread.
        await Task.Run(() => WaitForSingleObject(_hProcess, INFINITE)).ConfigureAwait(false);
        GetExitCodeProcess(_hProcess, out uint exitCode);
        ExitCode = (int)exitCode;

        // Close pseudo console so the output pipe sees EOF.
        if (_hPC != IntPtr.Zero)
        {
            ClosePseudoConsole(_hPC);
            _hPC = IntPtr.Zero;
        }

        await readTask.ConfigureAwait(false);

        return new WinGetCliRunner.WinGetCliResult(
            ExitCode,
            stdoutBuilder.ToString(),
            string.Empty); // ConPTY merges stderr into stdout
    }

    // VT100 parser states
    private enum VtState { Normal, EscSeen, Csi, Osc }

    // Safety limits — abandon malformed sequences that exceed these lengths
    private const int MaxCsiLen = 128;
    private const int MaxOscLen = 1024;

    /// <summary>
    /// Reads from the ConPTY output pipe, stripping VT100 escape sequences.
    /// Uses a state machine to parse CSI, OSC, and single-char escapes.
    /// \r and \x1b[G / \x1b[1G are treated as progress-line indicators.
    /// Progress lines are post-processed to fill in the unfilled bar track
    /// (winget uses cursor positioning + background colors for this area,
    /// which are invisible without terminal color support).
    /// </summary>
    private static void ReadConPtyOutput(
        IntPtr pipeHandle,
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

                // Normal mode
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

    /// <summary>
    /// Emits the current line as a progress (transient) line if non-empty.
    /// Applies progress bar post-processing to fill in the unfilled track.
    /// </summary>
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

    /// <summary>
    /// Winget's VT progress bar is 30 cells: filled cells use █ (U+2588) and
    /// partial blocks (U+2589–U+258F), while unfilled cells are rendered via
    /// background color or cursor positioning — both invisible after VT stripping.
    /// This method detects the bar and inserts ░ (U+2591) for the unfilled track.
    /// </summary>
    private static string FillProgressBarTrack(string line)
    {
        // Find the contiguous run of block characters (U+2588–U+258F)
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
                break; // End of contiguous block run
            }
        }

        if (barStart < 0)
            return line; // No block characters — not a progress bar

        int filledCount = barEnd - barStart + 1;

        // Winget bar width is 30 cells
        const int BarWidth = 30;
        int unfilledCount = BarWidth - filledCount;

        if (unfilledCount <= 0)
            return line; // Bar is full or wider — no track to add

        // Find where the text content starts after the bar area.
        // Skip any trailing spaces (these are the invisible unfilled area
        // or separator whitespace from the stripped VT output).
        int afterBar = barEnd + 1;
        while (afterBar < line.Length && line[afterBar] == ' ')
            afterBar++;

        // Rebuild: [prefix before bar][filled blocks][░ unfilled track][  text]
        var sb = new StringBuilder(line.Length + unfilledCount);
        sb.Append(line, 0, barEnd + 1);          // Everything up to and including last block
        sb.Append('\u2591', unfilledCount);        // ░ unfilled track
        if (afterBar < line.Length)
        {
            sb.Append("  ");                       // Separator
            sb.Append(line, afterBar, line.Length - afterBar); // Remaining text
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hPC != IntPtr.Zero)
        {
            ClosePseudoConsole(_hPC);
            _hPC = IntPtr.Zero;
        }

        if (_attrList != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(_attrList);
            Marshal.FreeHGlobal(_attrList);
            _attrList = IntPtr.Zero;
        }

        if (_hThread != IntPtr.Zero) { CloseHandle(_hThread); _hThread = IntPtr.Zero; }
        if (_hProcess != IntPtr.Zero) { CloseHandle(_hProcess); _hProcess = IntPtr.Zero; }
        if (_pipeWeWriteToConsole != IntPtr.Zero) { CloseHandle(_pipeWeWriteToConsole); _pipeWeWriteToConsole = IntPtr.Zero; }
        if (_pipeWeReadFromConsole != IntPtr.Zero) { CloseHandle(_pipeWeReadFromConsole); _pipeWeReadFromConsole = IntPtr.Zero; }
    }
}
