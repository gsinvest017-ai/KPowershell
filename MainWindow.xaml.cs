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

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
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
        coreWv.WebMessageReceived    += OnWebMessageReceived;
        coreWv.NavigationCompleted   += OnNavigationCompleted;
        coreWv.Navigate("https://app.local/terminal.html");
    }

    private void OnNavigationCompleted(object? sender,
        CoreWebView2NavigationCompletedEventArgs e) { }

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
                    FindTab(msg.id)?.Pty?.WriteInput(msg.data);
                break;
            case "resize":
                if (msg.id is not null && msg.cols > 0 && msg.rows > 0)
                    FindTab(msg.id)?.Pty?.Resize((short)msg.cols, (short)msg.rows);
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
        PostJs(new { type = "create", id = tab.Id });

        var pty = new ConPtyService();
        pty.OutputReceived += b64 =>
            Dispatcher.InvokeAsync(() =>
                TerminalWebView.CoreWebView2?.ExecuteScriptAsync(
                    $"writeToTerminal('{EscJs(tab.Id)}', '{b64}');"));

        try { pty.Start(); }
        catch (Exception ex)
        {
            PostJs(new { type = "write", id = tab.Id,
                data = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(
                        $"\r\n\x1b[31mFailed to start shell: {ex.Message}\x1b[0m\r\n")) });
            return;
        }
        tab.Pty = pty;
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
        else PostJs(new { type = "switch", id = tab.Id });
    }

    // ─────────────────────────────────────────────────────────────
    // Rename TextBox event handlers (掛在 Style EventSetter)
    // ─────────────────────────────────────────────────────────────

    /// <summary>TextBox 變為可見時自動 focus 並全選。</summary>
    private void RenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && (bool)e.NewValue)
        {
            // BeginInvoke 確保 layout 完成後才操作 focus
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                tb.Focus();
                tb.SelectAll();
            });
        }
    }

    /// <summary>Enter/Tab → 確認；Esc → 取消。</summary>
    private void RenameBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        switch (e.Key)
        {
            case Key.Return:
            case Key.Tab:
                CommitRename(tb);
                e.Handled = true;
                break;
            case Key.Escape:
                CancelRename(tb);
                e.Handled = true;
                break;
        }
    }

    /// <summary>點到其他地方（LostFocus）→ 確認。</summary>
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
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private void PostJs(object payload) =>
        TerminalWebView.CoreWebView2?.PostWebMessageAsString(JsonSerializer.Serialize(payload));

    private PsTab? FindTab(string id) =>
        _vm.Groups.SelectMany(g => g.Tabs).FirstOrDefault(t => t.Id == id);

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
    public int     line { get; set; }
    public int     cols { get; set; }
    public int     rows { get; set; }
}
