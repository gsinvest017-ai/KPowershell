# 進度：分割窗格 (Split Pane)

## 目標
在 KPowershell 的 Tab 內支援像 Windows Terminal 一樣的分割視窗：
- Alt+Shift+= → 垂直分割（左右）
- Alt+Shift+- → 水平分割（上下）
- Alt+Shift+W → 關閉活動分割窗格
- 分割條可拖曳調整比例；點擊窗格切換鍵盤焦點

## 計畫 Milestones

| # | 標題 | 預期產出 |
|---|------|---------|
| M1 | Model: SplitDirection + PsTab 分割欄位 | PsTab.OpenSplit / CloseSplit |
| M2 | terminal.html 重構：flex 分割佈局 + JS hotkey 攔截 | setLayout / makeBar / hotkey |
| M3 | C# 橋接：SpawnSplitPane / 路由 / 快捷鍵 | hotkey handler, paneactive, FindPty |
| M4 | Build & Deploy | AppData\Local\PsTabGroups 更新 |

---

## M1 — Model
<!-- 待補 -->

## M2 — terminal.html
<!-- 待補 -->

## M3 — C# 橋接
<!-- 待補 -->

## M4 — Build & Deploy
<!-- 待補 -->
