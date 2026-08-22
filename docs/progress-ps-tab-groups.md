---
type: progress
updated: 2026-06-06
repos: [KPowershell]
owner: User
---

# PsTabGroups 進度日誌

## 目標
建立 WPF + ConPTY + WebView2/xterm.js 的 PowerShell Tab 群組應用程式，
實作 Chrome 風格的 tab group 收折功能，並部署到系統（桌面捷徑 + PATH）。

## 計畫 Milestone

| # | 標題 | 狀態 | 預期產出 |
|---|------|:----:|---------|
| M1 | Repo + 骨架       | ✅ | git init、WPF 專案、NuGet 套件、目錄結構 |
| M2 | Models + Services | ✅ | TabGroup/PsTab 模型、ConPtyService（P/Invoke）、MainViewModel |
| M3 | UI 元件           | ✅ | TabGroupBar XAML、4 Converter、terminal.html（xterm.js 5.3） |
| M4 | 整合 + build      | ✅ | MainWindow 接線、WebView2↔ConPTY 橋接；0 error |
| M5 | 部署              | ✅ | Release publish、桌面捷徑、加使用者 PATH |

## 快速啟動
```powershell
# 直接執行（需重開 terminal 讓 PATH 生效）
PsTabGroups

# 或從桌面點 PsTabGroups.lnk
```

## 重新 build / 更新
```powershell
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
Set-Location "C:\Users\User\PsTabGroups"
& $dotnet publish -c Release -o publish --self-contained false
Copy-Item publish\* "C:\Users\User\AppData\Local\PsTabGroups\" -Recurse -Force
```

## Fallback 指引
若需中途接手，最少需要的步驟：
```powershell
cd C:\Users\User\PsTabGroups
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build
& $dotnet run
```

---

## M1 — Repo + 骨架
**完成時間**：2026-06-06
**產出**：git repo、WPF .NET 8 專案、WebView2 1.0.3967、CommunityToolkit.Mvvm 8.4.2
**決策**：使用 WebView2 + xterm.js 做終端渲染，避免 VtNetCore 的 WPF 整合複雜度；
Porta.Pty NuGet 版本不存在，改用直接 ConPTY P/Invoke（kernel32）。

## M2 — Models + Services + ViewModel
**完成時間**：2026-06-06
**產出**：
- `Models/TabGroup.cs`：ObservableObject + ToggleCollapseCommand
- `Models/PsTab.cs`：每個 tab 持有 ConPtyService 實例
- `Services/ConPtyService.cs`：CreatePseudoConsole / P/Invoke / ReadLoop / base64 output
- `ViewModels/MainViewModel.cs`：Groups 集合、NewTabInActiveGroup、NewGroup、CloseTab、ActivateTab

## M3 — UI 元件
**完成時間**：2026-06-06
**產出**：
- `Converters/` 4 個（BoolToArrow、InvertBoolVisibility、HexColor、BoolToTabBackground）
- `Resources/terminal.html`：xterm.js 5.3 + xterm-addon-fit 0.8、多 terminal 多路切換、WebView2 postMessage 橋接

## M4 — MainWindow 整合 + 編譯成功
**完成時間**：2026-06-06
**產出**：
- `MainWindow.xaml`：Chrome 風格 TabGroupBar（群組 label 點擊收折、tab 左鍵切換、中鍵關閉）
- `MainWindow.xaml.cs`：WebView2 初始化、ConPTY spawn、JS postMessage 橋接
- `App.xaml`：全域 Converter 資源
- Build：0 error，0 warning

## M5 — Release Publish + 部署
**完成時間**：2026-06-06
**安裝位置**：`C:\Users\User\AppData\Local\PsTabGroups\`
**桌面捷徑**：`C:\Users\User\Desktop\PsTabGroups.lnk`
**PATH**：已加入使用者 PATH（需重開 terminal 生效）
