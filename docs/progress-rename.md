---
type: progress
updated: 2026-06-06
repos: [KPowershell]
owner: User
---

# 進度：Group / Tab 雙擊重新命名

## 目標
Group 標籤與 Tab 標題皆可雙擊進入 inline TextBox 編輯，
Enter/Tab/失焦 確認，Esc 取消。

## Milestone 計畫
| # | 標題 | 預期產出 |
|---|------|---------|
| M1 | Model 層 | TabGroup/PsTab 加 IsEditing、EditingName、StartRename/Confirm/Cancel commands |
| M2 | XAML inline TextBox | Group 箭頭分離 collapse，name 雙擊 rename；Tab title 同理 |
| M3 | Code-behind + build + deploy | focus/SelectAll/KeyDown/LostFocus handler；0 error build；部署 |

## Fallback
```powershell
cd C:\Users\User\PsTabGroups
git log --oneline   # 找最近 working commit
git checkout <hash> -- Models/TabGroup.cs Models/PsTab.cs MainWindow.xaml MainWindow.xaml.cs
```
