---
type: progress
updated: 2026-06-08
repos: [KPowershell]
owner: User
---

# 進度：Profile/Theme 修正 + N-pane 分割 + Tab 拖曳

## 目標
1. ConPtyService 缺少 TERM/COLORTERM env vars，導致 Oh My Posh matrix theme 無法顯示
2. PsTab 只支援一次分割（最多 2 panes），改為 N-pane list
3. Tab 可透過滑鼠拖曳到其他 Group（跨 Group 移動 tab）

## Milestones

| # | 標題 | 狀態 |
|---|------|------|
| M1 | ConPtyService: 加入終端機 env vars | ⬜ |
| M2 | PsPane model + PsTab N-pane 重構 | ⬜ |
| M3 | terminal.html N-pane setLayout | ⬜ |
| M4 | C# 橋接更新（N-pane 路由/快捷鍵） | ⬜ |
| M5 | Tab 拖曳跨 Group | ⬜ |
| M6 | Build & Deploy | ⬜ |
