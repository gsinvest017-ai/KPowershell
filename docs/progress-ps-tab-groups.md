# PsTabGroups 進度日誌

## 目標
建立 WPF + ConPTY + WebView2/xterm.js 的 PowerShell Tab 群組應用程式，
實作 Chrome 風格的 tab group 收折功能，並部署到系統（桌面捷徑 + PATH）。

## 計畫 Milestone

| # | 標題 | 預期產出 |
|---|------|---------|
| M1 | Repo + 骨架 | git init、WPF 專案、NuGet 套件、目錄結構、progress 檔 |
| M2 | Models + Services | TabGroup/PsTab 模型、ConPtyService（P/Invoke）、MainViewModel |
| M3 | UI 元件 | TabGroupBar、Converters、terminal.html（xterm.js） |
| M4 | 整合 + 編譯 | MainWindow 接線、WebView2 ↔ ConPTY 橋接、成功 build |
| M5 | 部署 | dotnet publish Release、桌面捷徑、加 PATH |

## Fallback 指引
若需中途接手，最少需要的步驟：
```powershell
cd C:\Users\User\PsTabGroups
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
& $dotnet build
& $dotnet run
# 或
& $dotnet publish -c Release -o publish
```

---

## M1 — Repo + 骨架
**完成時間**：2026-06-06
**產出**：git repo、WPF .NET 8 專案、NuGet 套件（WebView2 1.0.3967、CommunityToolkit.Mvvm 8.4.2、Porta.Pty 1.0.7）
**決策**：使用 WebView2 + xterm.js 做終端渲染，避免 VtNetCore 的 WPF 整合複雜度；Porta.Pty 無法安裝時改用直接 ConPTY P/Invoke。
