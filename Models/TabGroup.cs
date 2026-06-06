using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PsTabGroups.Models;

public partial class TabGroup : ObservableObject
{
    [ObservableProperty] string name = "Group";
    [ObservableProperty] string colorHex = "#4ECDC4";
    [ObservableProperty] bool isCollapsed;

    public ObservableCollection<PsTab> Tabs { get; } = new();

    [RelayCommand]
    public void ToggleCollapse() => IsCollapsed = !IsCollapsed;

    public string CollapseArrow => IsCollapsed ? "▶" : "▼";

    partial void OnIsCollapsedChanged(bool value)
        => OnPropertyChanged(nameof(CollapseArrow));
}
