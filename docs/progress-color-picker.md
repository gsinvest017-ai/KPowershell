---
type: progress
updated: 2026-06-06
repos: [KPowershell]
owner: User
---

# 進度：Group / Tab 顏色選擇器

## 目標
讓使用者能夠右鍵點擊 Group 標籤或 Tab 標題，從 12 色調色盤即時修改顏色，
外觀行為貼近 Windows Terminal / Chrome tab-group 的顏色選擇 UX。

## 計畫 Milestones

| # | 標題 | 預期產出 |
|---|------|---------|
| M1 | Model 層 ColorHex + ChangeColor | `PsTab.ColorHex`, `TabGroup/PsTab.ChangeColor()` |
| M2 | Popup 色盤 UI + 事件接線 | 右鍵彈出 Popup，點色塊立即改色 |
| M3 | Build & Deploy | 重新發布並更新系統安裝版本 |

---

## M1 — Model 層擴充
commit 74bc961

- `PsTab` 新增 `ColorHex` ObservableProperty（預設 #4ECDC4）
- `PsTab` / `TabGroup` 新增 `ChangeColor(hex)` 方法
- `MainViewModel.AddTabToGroup` 建立 tab 時帶入 `group.ColorHex`

## M2 — UI Popup 色盤
commit M2 hash（見 git log）

- `MainWindow.xaml`: 新增 `ColorSwatchStyle`（圓形 24px 色塊 + hover 白框）
- `Popup x:Name="ColorPickerPopup"`：12 色調色盤，StaysOpen=False，Bottom 定位
- Group 標籤 Border：`MouseRightButtonUp="GroupLabel_RightClick"`
- Tab Border：`BorderBrush` 改綁 `PsTab.ColorHex`，加 `MouseRightButtonUp="TabBorder_RightClick"`
- `MainWindow.xaml.cs`: `_applyColor` Action，`ShowColorPicker` / `ColorSwatch_Click`

## M3 — Build & Deploy
- `dotnet publish -c Release` → `publish/`
- 複製到 `AppData\Local\PsTabGroups\`，KPowershell.exe 已更新

---

## Fallback 指引

若需回滾到某個 milestone，執行：
```powershell
git log --oneline -10   # 找到目標 hash
git checkout <hash> -- .
```
