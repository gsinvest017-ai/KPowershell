using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PsTabGroups.Services;

namespace PsTabGroups.Models;

public partial class PsTab : ObservableObject, IDisposable
{
    private static int _nextId = 1;

    public string Id { get; } = $"tab-{_nextId++}";
    public string GroupName { get; set; } = "";

    [ObservableProperty] string title = "pwsh";
    [ObservableProperty] bool isActive;
    [ObservableProperty] bool isEditing;
    [ObservableProperty] string editingName = "";

    public ConPtyService? Pty { get; set; }

    [RelayCommand]
    public void StartRename()
    {
        EditingName = Title;
        IsEditing   = true;
    }

    [RelayCommand]
    public void ConfirmRename()
    {
        if (!string.IsNullOrWhiteSpace(EditingName))
            Title = EditingName.Trim();
        IsEditing = false;
    }

    [RelayCommand]
    public void CancelRename()
    {
        IsEditing = false;
    }

    public void Dispose()
    {
        Pty?.Dispose();
        Pty = null;
    }
}
