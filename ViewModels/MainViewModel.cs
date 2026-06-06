using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PsTabGroups.Models;

namespace PsTabGroups.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<TabGroup> Groups { get; } = new();

    [ObservableProperty] PsTab? activeTab;
    [ObservableProperty] TabGroup? activeGroup;

    private readonly string[] _groupColors = new[]
    {
        "#4ECDC4", "#FF6B35", "#A78BFA", "#34D399", "#F59E0B",
        "#EC4899", "#60A5FA", "#F97316", "#10B981", "#8B5CF6"
    };
    private int _colorIndex;

    public MainViewModel()
    {
        // 預設第一個群組
        var defaultGroup = CreateGroup("Default");
        AddTabToGroup(defaultGroup);
    }

    public TabGroup CreateGroup(string name = "")
    {
        var g = new TabGroup
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Group {Groups.Count + 1}" : name,
            ColorHex = _groupColors[_colorIndex++ % _groupColors.Length]
        };
        Groups.Add(g);
        ActiveGroup = g;
        return g;
    }

    public PsTab AddTabToGroup(TabGroup group)
    {
        // 停用目前 active tab
        if (ActiveTab is not null) ActiveTab.IsActive = false;

        var tab = new PsTab { Title = "pwsh", GroupName = group.Name, ColorHex = group.ColorHex };
        group.Tabs.Add(tab);
        tab.IsActive = true;
        ActiveTab = tab;
        ActiveGroup = group;
        return tab;
    }

    [RelayCommand]
    public void ActivateTab(PsTab tab)
    {
        if (ActiveTab == tab) return;
        if (ActiveTab is not null) ActiveTab.IsActive = false;
        tab.IsActive = true;
        ActiveTab = tab;

        // 同步 active group
        ActiveGroup = Groups.FirstOrDefault(g => g.Tabs.Contains(tab));
    }

    [RelayCommand]
    public void NewTabInActiveGroup()
    {
        var group = ActiveGroup ?? Groups.FirstOrDefault();
        if (group is null) group = CreateGroup();
        AddTabToGroup(group);
    }

    [RelayCommand]
    public void NewGroup()
    {
        var g = CreateGroup();
        AddTabToGroup(g);
    }

    [RelayCommand]
    public void CloseTab(PsTab tab)
    {
        var group = Groups.FirstOrDefault(g => g.Tabs.Contains(tab));
        if (group is null) return;

        group.Tabs.Remove(tab);
        tab.Dispose();

        if (group.Tabs.Count == 0)
        {
            Groups.Remove(group);
            // 如果沒有任何 group，建一個新的
            if (Groups.Count == 0) { var g = CreateGroup(); AddTabToGroup(g); return; }
        }

        // 選下一個可用的 tab
        var next = group.Tabs.LastOrDefault()
            ?? Groups.SelectMany(g => g.Tabs).LastOrDefault();
        if (next is not null) ActivateTab(next);
    }
}
