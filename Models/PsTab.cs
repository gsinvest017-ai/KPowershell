using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PsTabGroups.Services;

namespace PsTabGroups.Models;

public enum SplitDirection { None, Vertical, Horizontal }

public partial class PsTab : ObservableObject, IDisposable
{
    private static int _nextId  = 1;
    private int        _nextNum = 2;   // 下一個分割窗格的序號

    public string Id        { get; } = $"tab-{_nextId++}";
    public string GroupName { get; set; } = "";

    [ObservableProperty] string title      = "pwsh";
    [ObservableProperty] bool   isActive;
    [ObservableProperty] bool   isEditing;
    [ObservableProperty] string editingName = "";
    [ObservableProperty] string colorHex    = "#4ECDC4";

    // ── 分割窗格 ────────────────────────────────────────────────
    public List<PsPane>   Panes         { get; } = new();
    public SplitDirection SplitDir      { get; private set; } = SplitDirection.None;
    public int            ActivePaneIdx { get; set; } = 0;
    public PsPane?        ActivePane    =>
        Panes.Count > 0 ? Panes[Math.Max(0, Math.Min(ActivePaneIdx, Panes.Count - 1))] : null;
    public bool           IsSplit       => Panes.Count > 1;

    /// <summary>第一個窗格的 Pty（向下相容舊呼叫）。</summary>
    public ConPtyService? Pty
    {
        get => Panes.Count > 0 ? Panes[0].Pty : null;
        set { if (Panes.Count > 0) Panes[0].Pty = value; }
    }

    public PsTab()
    {
        Panes.Add(new PsPane(Id));  // 第一個窗格的 Id 與 tab Id 相同
    }

    /// <summary>在目前活動窗格之後新增一個分割窗格，回傳新的 PsPane。</summary>
    public PsPane AddPane(SplitDirection dir)
    {
        if (SplitDir == SplitDirection.None) SplitDir = dir;
        var pane = new PsPane($"{Id}-p{_nextNum++}");
        // 插入到活動窗格之後
        var insertAt = Math.Min(ActivePaneIdx + 1, Panes.Count);
        Panes.Insert(insertAt, pane);
        ActivePaneIdx = insertAt;
        return pane;
    }

    /// <summary>移除活動窗格；若只剩 1 個窗格則什麼都不做，回傳被移除的窗格（或 null）。</summary>
    public PsPane? RemoveActivePane()
    {
        if (Panes.Count <= 1) return null;
        var pane = Panes[ActivePaneIdx];
        pane.Dispose();
        Panes.RemoveAt(ActivePaneIdx);
        if (Panes.Count == 1) SplitDir = SplitDirection.None;
        ActivePaneIdx = Math.Max(0, ActivePaneIdx - 1);
        return pane;
    }

    /// <summary>所有窗格的 Id 列表（供 JS 佈局使用）。</summary>
    public string[] PaneIds() => Panes.Select(p => p.Id).ToArray();

    // ── Rename ──────────────────────────────────────────────────
    [RelayCommand]
    public void StartRename() { EditingName = Title; IsEditing = true; }

    [RelayCommand]
    public void ConfirmRename()
    {
        if (!string.IsNullOrWhiteSpace(EditingName)) Title = EditingName.Trim();
        IsEditing = false;
    }

    [RelayCommand]
    public void CancelRename() => IsEditing = false;

    public void ChangeColor(string hex) => ColorHex = hex;

    public void Dispose()
    {
        foreach (var p in Panes) p.Dispose();
        Panes.Clear();
    }
}
