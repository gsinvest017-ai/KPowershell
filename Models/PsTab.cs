using CommunityToolkit.Mvvm.ComponentModel;
using PsTabGroups.Services;

namespace PsTabGroups.Models;

public partial class PsTab : ObservableObject, IDisposable
{
    private static int _nextId = 1;

    public string Id { get; } = $"tab-{_nextId++}";
    public string GroupName { get; set; } = "";

    [ObservableProperty] string title = "pwsh";
    [ObservableProperty] bool isActive;

    public ConPtyService? Pty { get; set; }

    public void Dispose()
    {
        Pty?.Dispose();
        Pty = null;
    }
}
