using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PsTabGroups.Models;

public partial class TabGroup : ObservableObject
{
    [ObservableProperty] string name = "Group";
    [ObservableProperty] string colorHex = "#4ECDC4";
    [ObservableProperty] bool isCollapsed;
    [ObservableProperty] bool isEditing;
    [ObservableProperty] string editingName = "";

    public ObservableCollection<PsTab> Tabs { get; } = new();

    [RelayCommand]
    public void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    [RelayCommand]
    public void StartRename()
    {
        EditingName = Name;
        IsEditing   = true;
    }

    [RelayCommand]
    public void ConfirmRename()
    {
        if (!string.IsNullOrWhiteSpace(EditingName))
            Name = EditingName.Trim();
        IsEditing = false;
    }

    [RelayCommand]
    public void CancelRename()
    {
        IsEditing = false;
    }

    public string CollapseArrow => IsCollapsed ? "▶" : "▼";

    partial void OnIsCollapsedChanged(bool value)
        => OnPropertyChanged(nameof(CollapseArrow));
}
