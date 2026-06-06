using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using PsTabGroups.Models;
using PsTabGroups.Services;
using PsTabGroups.ViewModels;

namespace PsTabGroups;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _webReady;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();
        DataContext = _vm;

        // expose ActivateTab as ICommand on the window (XAML binds to DataContext.ActivateTabCommand)
        // 實際上 MainViewModel 已有 RelayCommand，直接掛 DataContext 即可，此處確保存在
        _vm.PropertyChanged += OnVmPropertyChanged;

        Loaded   += OnLoaded;
        Closing  += OnClosing;
    }

    // ─────────────────────────────────────────────────────────────
    // WebView2 初始化
    // ─────────────────────────────────────────────────────────────

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await TerminalWebView.EnsureCoreWebView2Async();

        var coreWv = TerminalWebView.CoreWebView2;

        // 允許 file:// 存取，並 map 虛擬 host 到 Resources 資料夾
        var resDir = Path.Combine(AppContext.BaseDirectory, "Resources");
        coreWv.SetVirtualHostNameToFolderMapping(
            "app.local", resDir,
            CoreWebView2HostResourceAccessKind.Allow);

        // 停用右鍵選單 & 狀態列
        coreWv.Settings.AreDefaultContextMenusEnabled = false;
        coreWv.Settings.IsStatusBarEnabled            = false;
        coreWv.Settings.AreBrowserAcceleratorKeysEnabled = false;

        coreWv.WebMessageReceived += OnWebMessageReceived;

        // 等 terminal.html 載好再建立第一個 terminal
        coreWv.NavigationCompleted += OnNavigationCompleted;
        coreWv.Navigate("https://app.local/terminal.html");
    }

    private void OnNavigationCompleted(object? sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        // 頁面載完後等 JS 送來 ready 訊息才建 terminal
        // (OnWebMessageReceived 處理 type=ready)
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
                // 頁面 ready，建立 ViewModel 裡已有的第一個 tab
                Dispatcher.Invoke(() => InitFirstTab());
                break;

            case "input":
                // xterm.js → ConPTY
                if (msg.id is not null && msg.data is not null)
                {
                    var tab = FindTab(msg.id);
                    tab?.Pty?.WriteInput(msg.data);
                }
                break;

            case "resize":
                if (msg.id is not null && msg.cols > 0 && msg.rows > 0)
                {
                    var tab = FindTab(msg.id);
                    tab?.Pty?.Resize((short)msg.cols, (short)msg.rows);
                }
                break;

            case "jserr":
                // JS 錯誤上報：彈 MessageBox 方便 debug
                Dispatcher.Invoke(() =>
                    System.Windows.MessageBox.Show(
                        $"JS Error: {msg.msg}\n{msg.src}:{msg.line}",
                        "KPowershell – JS Error",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning));
                break;
        }
    }

    private void InitFirstTab()
    {
        var tab = _vm.Groups.SelectMany(g => g.Tabs).FirstOrDefault();
        if (tab is not null)
            SpawnTerminal(tab);
    }

    // ─────────────────────────────────────────────────────────────
    // 建立 terminal（JS + ConPTY）
    // ─────────────────────────────────────────────────────────────

    private void SpawnTerminal(PsTab tab)
    {
        if (!_webReady) return;

        // 通知 xterm.js 建立 terminal 實例
        PostJs(new { type = "create", id = tab.Id });

        // 建立 ConPTY
        var pty = new ConPtyService();
        pty.OutputReceived += b64 =>
        {
            // background thread → dispatch to UI thread for ExecuteScriptAsync
            Dispatcher.InvokeAsync(() =>
                TerminalWebView.CoreWebView2?.ExecuteScriptAsync(
                    $"writeToTerminal('{EscJs(tab.Id)}', '{b64}');"));
        };

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
    // ViewModel 監聽：ActiveTab 切換
    // ─────────────────────────────────────────────────────────────

    private void OnVmPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.ActiveTab)) return;

        var tab = _vm.ActiveTab;
        if (tab is null) return;

        if (!_webReady) return;

        // 若 tab 尚未有 PTY，先建立
        if (tab.Pty is null)
            SpawnTerminal(tab);
        else
            PostJs(new { type = "switch", id = tab.Id });
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private void PostJs(object payload) =>
        TerminalWebView.CoreWebView2?.PostWebMessageAsString(
            JsonSerializer.Serialize(payload));

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

// JSON message DTO
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
