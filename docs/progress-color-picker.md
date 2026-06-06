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
<!-- 待補 commit hash -->

## M2 — UI Popup 色盤
<!-- 待補 commit hash -->

## M3 — Build & Deploy
<!-- 待補 commit hash -->

---

## Fallback 指引

若需回滾到某個 milestone，執行：
```powershell
git log --oneline -10   # 找到目標 hash
git checkout <hash> -- .
```
