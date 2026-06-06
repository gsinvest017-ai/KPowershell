using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PsTabGroups.Services;

/// <summary>
/// ConPTY (Windows 10 1809+) P/Invoke 橋接層。
/// 每個 PsTab 擁有一個 ConPtyService 實例。
/// </summary>
public sealed class ConPtyService : IDisposable
{
    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD { public short X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
        public int dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int CreatePseudoConsole(COORD size,
        SafeFileHandle hInput, SafeFileHandle hOutput, uint dwFlags, out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll")]
    private static extern void ClosePseudoConsole(IntPtr hPC);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out SafeFileHandle hReadPipe,
        out SafeFileHandle hWritePipe, IntPtr lpPipeAttributes, int nSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string? lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
        bool bInheritHandles, uint dwCreationFlags,
        IntPtr lpEnvironment, string? lpCurrentDirectory,
        [In] ref STARTUPINFOEX lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList, uint dwFlags, IntPtr Attribute,
        IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint CREATE_UNICODE_ENVIRONMENT  = 0x00000400;
    private static readonly IntPtr ATTR_PSEUDOCONSOLE = new(0x00020016);

    #endregion

    private IntPtr _hPC = IntPtr.Zero;
    private IntPtr _attrList = IntPtr.Zero;
    private PROCESS_INFORMATION _proc;
    private SafeFileHandle? _inputPipe;
    private SafeFileHandle? _outputPipe;
    private FileStream? _inputStream;
    private FileStream? _outputStream;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>每次讀到輸出時觸發，payload 為 base64 編碼的 raw bytes。</summary>
    public event Action<string>? OutputReceived;

    public void Start(short cols = 220, short rows = 50)
    {
        if (!CreatePipe(out SafeFileHandle ptyInRead, out SafeFileHandle ptyInWrite, IntPtr.Zero, 0))
            throw new InvalidOperationException($"CreatePipe(in) failed: {Marshal.GetLastWin32Error()}");
        if (!CreatePipe(out SafeFileHandle ptyOutRead, out SafeFileHandle ptyOutWrite, IntPtr.Zero, 0))
            throw new InvalidOperationException($"CreatePipe(out) failed: {Marshal.GetLastWin32Error()}");

        int hr = CreatePseudoConsole(new COORD { X = cols, Y = rows }, ptyInRead, ptyOutWrite, 0, out _hPC);
        ptyInRead.Dispose();
        ptyOutWrite.Dispose();
        if (hr != 0) throw new InvalidOperationException($"CreatePseudoConsole: 0x{hr:X8}");

        _inputPipe  = ptyInWrite;
        _outputPipe = ptyOutRead;

        // 建立 Attribute List（PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE）
        IntPtr attrSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
        _attrList = Marshal.AllocHGlobal(attrSize);
        if (!InitializeProcThreadAttributeList(_attrList, 1, 0, ref attrSize))
            throw new InvalidOperationException($"InitializeProcThreadAttributeList failed: {Marshal.GetLastWin32Error()}");
        if (!UpdateProcThreadAttribute(_attrList, 0, ATTR_PSEUDOCONSOLE,
                _hPC, new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
            throw new InvalidOperationException($"UpdateProcThreadAttribute failed: {Marshal.GetLastWin32Error()}");

        var si = new STARTUPINFOEX();
        si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
        si.lpAttributeList = _attrList;

        string shell = FindShell();
        if (!CreateProcess(null, shell, IntPtr.Zero, IntPtr.Zero, false,
                EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                IntPtr.Zero, null, ref si, out _proc))
            throw new InvalidOperationException($"CreateProcess failed: {Marshal.GetLastWin32Error()}");

        // CreatePipe() 建立的匿名 pipe 不支援 Overlapped I/O，兩端都必須 isAsync:false
        _inputStream  = new FileStream(_inputPipe,  FileAccess.Write, bufferSize: 1024, isAsync: false);
        _outputStream = new FileStream(_outputPipe, FileAccess.Read,  bufferSize: 1024, isAsync: false);

        _cts = new CancellationTokenSource();
        _ = ReadLoopAsync(_cts.Token);
    }

    private static string FindShell()
    {
        foreach (var candidate in new[] { "pwsh.exe", "powershell.exe" })
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
            foreach (var dir in paths)
            {
                var full = Path.Combine(dir.Trim(), candidate);
                if (File.Exists(full)) return full;
            }
            // 常見安裝位置
            var prog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PowerShell", "7", candidate);
            if (File.Exists(prog)) return prog;
        }
        return "powershell.exe";
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buf = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // 匿名 pipe 無 Overlapped I/O — 用 Task.Run 包同步 Read 避免阻塞 UI thread
                int n = await Task.Run(() => _outputStream!.Read(buf, 0, buf.Length), ct);
                if (n == 0) break;
                OutputReceived?.Invoke(Convert.ToBase64String(buf, 0, n));
            }
        }
        catch (OperationCanceledException) { }
        catch { /* process exited */ }
    }

    public void WriteInput(string text)
    {
        if (_inputStream is null) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        _inputStream.Write(bytes, 0, bytes.Length);
        _inputStream.Flush();
    }

    public void Resize(short cols, short rows)
    {
        if (_hPC != IntPtr.Zero)
            ResizePseudoConsole(_hPC, new COORD { X = cols, Y = rows });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        try { _inputStream?.Dispose(); } catch { }
        try { _outputStream?.Dispose(); } catch { }
        if (_hPC != IntPtr.Zero) { ClosePseudoConsole(_hPC); _hPC = IntPtr.Zero; }
        if (_attrList != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(_attrList);
            Marshal.FreeHGlobal(_attrList);
            _attrList = IntPtr.Zero;
        }
        if (_proc.hProcess != IntPtr.Zero) CloseHandle(_proc.hProcess);
        if (_proc.hThread  != IntPtr.Zero) CloseHandle(_proc.hThread);
    }
}
