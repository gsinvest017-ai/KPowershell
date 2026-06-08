using PsTabGroups.Services;

namespace PsTabGroups.Models;

/// <summary>Tab 內的單一終端機窗格（ConPTY + xterm.js 實例）。</summary>
public sealed class PsPane : IDisposable
{
    public string         Id  { get; }
    public ConPtyService? Pty { get; set; }

    public PsPane(string id) { Id = id; }

    public void Dispose() { Pty?.Dispose(); Pty = null; }
}
