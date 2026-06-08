using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using PsTabGroups.Models;
using PsTabGroups.Services;
using PsTabGroups.ViewModels;

namespace PsTabGroups;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _webReady;
    private Action<string>? _applyColor;
    // 等待 xterm.js 回報實際尺寸後才啟動 ConPTY，避免初始尺寸不符造成重複 redraw
    private readonly HashSet<string> _pendingPaneSpawns = new();

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;

        // 訂閱 tab 關閉事件，清理 JS 端的 xterm 實例
        _vm.Groups.CollectionChanged += OnGroupsChanged;
        foreach (var g in _vm.Groups) g.Tabs.CollectionChanged += OnTabsChanged;

        Loaded  += OnLoaded;
        Closing += OnClosing;
    }

    // ─────────────────────────────────────────────────────────────
    // WebView2
    // ─────────────────────────────────────────────────────────────

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await TerminalWebView.EnsureCoreWebView2Async();
        var coreWv = TerminalWebView.CoreWebView2;
        var resDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        coreWv.SetVirtualHostNameToFolderMapping("app.local", resDir,
            CoreWebView2HostResourceAccessKind.Allow);
        coreWv.Settings.AreDefaultContextMenusEnabled    = false;
        coreWv.Settings.IsStatusBarEnabled               = false;
        coreWv.Settings.AreBrowserAcceleratorKeysEnabled = false;
        coreWv.WebMessageReceived += OnWebMessageReceived;
        coreWv.Navigate("https://app.local/terminal.html");
    }

    // ─────────────────────────────────────────────────────────────
    // WebView2 ↔ C# 訊息橋
    // ─────────────────────────────────────────────────────────────

    private void OnWebMessageReceived(object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.TryGetWebMessageAsString();
        TerminalMessage? msg;
        try { msg = JsonSerializer.Deserialize<TerminalMessage>(json); }
        catch { return; }
        if (msg is null) return;

        switch (msg.type)
        {
            case "ready":
                _webReady = true;
                Dispatcher.Invoke(InitFirstTab);
                break;

            case "input":
                if (msg.id is not null && msg.data is not null)
                {
                    FindPty(msg.id)?.WriteInput(msg.data);
                    // 記錄哪個 pane 有輸入焦點
                    var inputTab = FindTabContaining(msg.id);
                    if (inputTab is not null)
                    {
                        var idx = inputTab.Panes.FindIndex(p => p.Id == msg.id);
                        if (idx >= 0) inputTab.ActivePaneIdx = idx;
                    }
                }
                break;

            case "resize":
                if (msg.id is not null && msg.cols > 0 && msg.rows > 0)
                {
                    if (_pendingPaneSpawns.Remove(msg.id))
                    {
                        // xterm.js 回報了實際尺寸 → 以正確初始大小啟動 ConPTY
                        var pane = FindPaneById(msg.id);
                        if (pane is not null) SpawnPtyForPane(pane, (short)msg.cols, (short)msg.rows);
                    }
                    else
                    {
                        FindPty(msg.id)?.Resize((short)msg.cols, (short)msg.rows);
                    }
                }
                break;

            case "paneactive":
                // 使用者點擊分割窗格 → 更新 ActivePaneIdx
                if (msg.id is not null)
                {
                    var tab = FindTabContaining(msg.id);
                    if (tab is not null)
                    {
                        var idx = tab.Panes.FindIndex(p => p.Id == msg.id);
                        if (idx >= 0) tab.ActivePaneIdx = idx;
                    }
                }
                break;

            case "hotkey":
                Dispatcher.Invoke(() => HandleHotkey(msg.key));
                break;

            case "jserr":
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"JS Error: {msg.msg}\n{msg.src}:{msg.line}",
                        "KPowershell – JS Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning));
                break;
        }
    }

    private void InitFirstTab()
    {
        var tab = _vm.Groups.SelectMany(g => g.Tabs).FirstOrDefault();
        if (tab is not null) SpawnTerminal(tab);
    }

    // ─────────────────────────────────────────────────────────────
    // Terminal spawn
    // ─────────────────────────────────────────────────────────────

    private void SpawnTerminal(PsTab tab)
    {
        if (!_webReady) return;
        var pane = tab.Panes[0];
        _pendingPaneSpawns.Add(pane.Id);
        PostJs(new { type = "create", id = pane.Id });
        // ConPTY 延遲到 xterm.js 回報實際尺寸（resize 訊息）後才啟動
    }

    private void SpawnSplitPane(PsTab tab, PsPane pane)
    {
        if (!_webReady) return;
        _pendingPaneSpawns.Add(pane.Id);
        PostJs(new { type      = "split",
                     panes     = tab.PaneIds(),
                     direction = tab.SplitDir.ToString().ToLower() });
        // 同上，等 resize 再啟動
    }

    private void SpawnPtyForPane(PsPane pane, short cols = 80, short rows = 24)
    {
        var pty = new ConPtyService();
        var capturedId = pane.Id;
        pty.OutputReceived += b64 =>
            Dispatcher.InvokeAsync(() =>
                TerminalWebView.CoreWebView2?.ExecuteScriptAsync(
                    $"writeToTerminal('{EscJs(capturedId)}', '{b64}');"));

        try { pty.Start(cols, rows); }
        catch (Exception ex)
        {
            PostJs(new { type = "write", id = capturedId,
                data = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(
                        $"\r\n\x1b[31mFailed to start shell: {ex.Message}\x1b[0m\r\n")) });
            return;
        }
        pane.Pty = pty;
    }

    // ─────────────────────────────────────────────────────────────
    // Split pane 快捷鍵處理
    // ─────────────────────────────────────────────────────────────

    private void HandleHotkey(string? key)
    {
        switch (key)
        {
            case "splitVertical":   SplitActive(SplitDirection.Vertical);   break;
            case "splitHorizontal": SplitActive(SplitDirection.Horizontal); break;
            case "closeSplitPane":  CloseSplitPane();                       break;
        }
    }

    private void SplitActive(SplitDirection dir)
    {
        var tab = _vm.ActiveTab;
        if (tab is null) return;
        // 若方向已確定且與請求不同，忽略（保持一致方向）
        if (tab.IsSplit && tab.SplitDir != dir) return;
        var newPane = tab.AddPane(dir);
        SpawnSplitPane(tab, newPane);
    }

    private void CloseSplitPane()
    {
        var tab = _vm.ActiveTab;
        if (tab is null || !tab.IsSplit) return;

        var pane = tab.ActivePane;
        if (pane is null) return;
        var paneId   = pane.Id;
        var focusIdx = tab.ActivePaneIdx;

        tab.RemoveActivePane();  // 移除並 dispose pane

        PostJs(new { type     = "closepane",
                     paneId,
                     panes    = tab.PaneIds(),
                     direction = tab.SplitDir.ToString().ToLower(),
                     focusIdx = Math.Max(0, focusIdx - 1) });
    }

    // ─────────────────────────────────────────────────────────────
    // ViewModel → ActiveTab 切換
    // ─────────────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.ActiveTab)) return;
        var tab = _vm.ActiveTab;
        if (tab is null || !_webReady) return;
        if (tab.Pty is null) SpawnTerminal(tab);
        else PostSwitchLayout(tab);
    }

    private void PostSwitchLayout(PsTab tab) =>
        PostJs(new { type      = "switch",
                     id        = tab.Id,
                     panes     = tab.PaneIds(),
                     direction = tab.SplitDir.ToString().ToLower() });

    // ─────────────────────────────────────────────────────────────
    // Tab 關閉清理
    // ─────────────────────────────────────────────────────────────

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (TabGroup g in e.NewItems) g.Tabs.CollectionChanged += OnTabsChanged;
        if (e.OldItems is not null)
            foreach (TabGroup g in e.OldItems) g.Tabs.CollectionChanged -= OnTabsChanged;
    }

    private void OnTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_webReady || e.OldItems is null) return;
        foreach (PsTab tab in e.OldItems)
            foreach (var pane in tab.Panes)
                PostJs(new { type = "remove", id = pane.Id });
    }

    // ─────────────────────────────────────────────────────────────
    // Rename TextBox handlers
    // ─────────────────────────────────────────────────────────────

    private void RenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && (bool)e.NewValue)
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
                () => { tb.Focus(); tb.SelectAll(); });
    }

    private void RenameBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        switch (e.Key)
        {
            case Key.Return: case Key.Tab: CommitRename(tb); e.Handled = true; break;
            case Key.Escape: CancelRename(tb); e.Handled = true;               break;
        }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) CommitRename(tb);
    }

    private static void CommitRename(TextBox tb)
    {
        switch (tb.DataContext)
        {
            case TabGroup g when g.IsEditing: g.ConfirmRenameCommand.Execute(null); break;
            case PsTab   t when t.IsEditing:  t.ConfirmRenameCommand.Execute(null); break;
        }
    }

    private static void CancelRename(TextBox tb)
    {
        switch (tb.DataContext)
        {
            case TabGroup g: g.CancelRenameCommand.Execute(null); break;
            case PsTab   t:  t.CancelRenameCommand.Execute(null); break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Color picker
    // ─────────────────────────────────────────────────────────────

    private void GroupLabel_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TabGroup g)
        {
            _applyColor = hex => g.ChangeColor(hex);
            ShowColorPicker(fe);
            e.Handled = true;
        }
    }

    private void TabBorder_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PsTab t)
        {
            _applyColor = hex => t.ChangeColor(hex);
            ShowColorPicker(fe);
            e.Handled = true;
        }
    }

    private void ShowColorPicker(FrameworkElement target)
    {
        ColorPickerPopup.PlacementTarget = target;
        ColorPickerPopup.IsOpen = true;
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            _applyColor?.Invoke(hex);
            _applyColor = null;
        }
        ColorPickerPopup.IsOpen = false;
    }

    // ─────────────────────────────────────────────────────────────
    // Tab Drag-and-Drop（跨 Group 移動）
    // ─────────────────────────────────────────────────────────────

    private Point _dragStart;
    private bool  _draggingTab;

    private void Tab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is PsTab)
        {
            _dragStart   = e.GetPosition(null);
            _draggingTab = true;
        }
    }

    private void Tab_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingTab || e.LeftButton != MouseButtonState.Pressed) { _draggingTab = false; return; }
        if (sender is not FrameworkElement fe || fe.DataContext is not PsTab tab) return;

        var now = e.GetPosition(null);
        if (Math.Abs(now.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(now.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        _draggingTab = false;
        var data = new DataObject("PsTab", tab);
        DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
    }

    private void Group_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("PsTab") ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Group_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not TabGroup targetGroup) return;
        if (!e.Data.GetDataPresent("PsTab")) return;
        var tab = (PsTab)e.Data.GetData("PsTab");

        // 找到原本的 group
        var srcGroup = _vm.Groups.FirstOrDefault(g => g.Tabs.Contains(tab));
        if (srcGroup is null || srcGroup == targetGroup) return;

        srcGroup.Tabs.Remove(tab);
        targetGroup.Tabs.Add(tab);
        tab.GroupName = targetGroup.Name;
        _vm.ActivateTab(tab);
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private void PostJs(object payload) =>
        TerminalWebView.CoreWebView2?.PostWebMessageAsString(JsonSerializer.Serialize(payload));

    private ConPtyService? FindPty(string id)
    {
        foreach (var g in _vm.Groups)
        foreach (var tab in g.Tabs)
        {
            var pane = tab.Panes.FirstOrDefault(p => p.Id == id);
            if (pane is not null) return pane.Pty;
        }
        return null;
    }

    private PsPane? FindPaneById(string id)
    {
        foreach (var g in _vm.Groups)
        foreach (var tab in g.Tabs)
        {
            var pane = tab.Panes.FirstOrDefault(p => p.Id == id);
            if (pane is not null) return pane;
        }
        return null;
    }

    private PsTab? FindTabContaining(string id) =>
        _vm.Groups.SelectMany(g => g.Tabs)
           .FirstOrDefault(t => t.Panes.Any(p => p.Id == id));

    private static string EscJs(string s) =>
        s.Replace("'", "\\'").Replace("\\", "\\\\");

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        foreach (var tab in _vm.Groups.SelectMany(g => g.Tabs))
            tab.Dispose();
    }
}

// ── JSON message DTO ──────────────────────────────────────────────
file class TerminalMessage
{
    public string? type { get; set; }
    public string? id   { get; set; }
    public string? data { get; set; }
    public string? msg  { get; set; }
    public string? src  { get; set; }
    public string? key  { get; set; }
    public int     line { get; set; }
    public int     cols { get; set; }
    public int     rows { get; set; }
}
