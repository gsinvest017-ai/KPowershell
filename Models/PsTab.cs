using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PsTabGroups.Services;

namespace PsTabGroups.Models;

public enum SplitDirection { None, Vertical, Horizontal }

public partial class PsTab : ObservableObject, IDisposable
{
    private static int _nextId = 1;

    public string Id { get; } = $"tab-{_nextId++}";
    public string GroupName { get; set; } = "";

    [ObservableProperty] string title = "pwsh";
    [ObservableProperty] bool isActive;
    [ObservableProperty] bool isEditing;
    [ObservableProperty] string editingName = "";
    [ObservableProperty] string colorHex = "#4ECDC4";

    public ConPtyService? Pty { get; set; }

    // ── Split pane ──────────────────────────────────────────────
    public string?        SplitPaneId  { get; private set; }
    public ConPtyService? SplitPty     { get; set; }
    public SplitDirection SplitDir     { get; private set; } = SplitDirection.None;
    public string         ActivePaneId { get; set; } = "";
    public bool           IsSplit      => SplitDir != SplitDirection.None;

    public PsTab() { ActivePaneId = Id; }

    public void OpenSplit(SplitDirection dir)
    {
        if (IsSplit) return;
        SplitPaneId  = $"{Id}-p2";
        SplitDir     = dir;
        ActivePaneId = SplitPaneId;
    }

    public void CloseSplit()
    {
        SplitPty?.Dispose();
        SplitPty     = null;
        SplitPaneId  = null;
        SplitDir     = SplitDirection.None;
        ActivePaneId = Id;
    }

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
        CloseSplit();
        Pty?.Dispose();
        Pty = null;
    }
}
