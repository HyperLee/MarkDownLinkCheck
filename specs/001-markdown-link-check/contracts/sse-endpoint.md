# 合約定義：SSE 串流端點

**Feature Branch**: `001-markdown-link-check`  
**建立日期**: 2026-02-23  
**狀態**: 完成

---

## 端點概覽

本專案為 Razor Pages Web 應用程式，對外暴露的介面為一個 SSE（Server-Sent Events）串流端點，用於執行連結檢測並即時回傳結果。

---

## POST /api/check/sse

### 說明

接受檢測請求，啟動 Markdown 連結檢測流程，透過 SSE 格式串流回傳檢測進度與結果。

### Request

**Content-Type**: `application/json`

**Body**:

```json
{
  "mode": "MarkdownSource" | "RepoUrl",
  "markdownContent": "string | null",
  "repoUrl": "string | null",
  "branch": "string | null"
}
```

| 欄位 | 型別 | 必填 | 驗證規則 |
|------|------|------|----------|
| `mode` | `string` | ✅ | `"MarkdownSource"` 或 `"RepoUrl"` |
| `markdownContent` | `string?` | 條件 | `mode == "MarkdownSource"` 時必填；≤ 100,000 字元；不可為空白 |
| `repoUrl` | `string?` | 條件 | `mode == "RepoUrl"` 時必填；格式 `https://github.com/{owner}/{repo}` |
| `branch` | `string?` | 選填 | 分支名稱；未指定時使用 Repo 預設分支 |

### Response

**Content-Type**: `text/event-stream`  
**Cache-Control**: `no-cache`  
**Connection**: `keep-alive`

#### SSE 事件格式

每則事件遵循標準 SSE 協議格式：

```
event: <event-type>
data: <json-payload>

```

（每則事件結尾為兩個換行 `\n\n`）

---

### 事件類型

#### 1. `progress` — 進度更新

檢測過程中持續推送，通知客戶端目前進度。

```
event: progress
data: {"checkedCount":3,"totalCount":10,"currentFile":"README.md"}
```

| 欄位 | 型別 | 說明 |
|------|------|------|
| `checkedCount` | `int` | 已檢查的連結數量 |
| `totalCount` | `int` | 連結總數 |
| `currentFile` | `string` | 正在處理的檔案名稱 |

---

#### 2. `file-result` — 單一檔案結果

每完成一個檔案的檢測即推送該檔案的結果（FR-039）。

```
event: file-result
data: {
  "fileName": "docs/setup.md",
  "relativePath": "docs/setup.md",
  "brokenCount": 2,
  "warningCount": 1,
  "healthyCount": 5,
  "skippedCount": 0,
  "linkResults": [
    {
      "targetUrl": "https://example.com/broken",
      "rawText": "[link](https://example.com/broken)",
      "lineNumber": 15,
      "linkType": "ExternalUrl",
      "status": "Broken",
      "httpStatusCode": 404,
      "errorType": null,
      "errorMessage": "Not Found",
      "redirectUrl": null,
      "anchorSuggestion": null,
      "durationMs": 1234
    },
    {
      "targetUrl": "#installatoin",
      "rawText": "[link](#installatoin)",
      "lineNumber": 23,
      "linkType": "Anchor",
      "status": "Broken",
      "httpStatusCode": null,
      "errorType": "anchor_not_found",
      "errorMessage": "anchor not found",
      "redirectUrl": null,
      "anchorSuggestion": "#installation",
      "durationMs": 0
    }
  ]
}
```

**linkResults 排序**：❌ Broken 在前 → ⚠️ Warning → ✅ Healthy / ⏭️ Skipped

---

#### 3. `complete` — 檢測完成

所有檔案檢測完畢後推送最終摘要報告。

```
event: complete
data: {
  "fileCount": 3,
  "totalLinkCount": 42,
  "healthyCount": 35,
  "brokenCount": 4,
  "warningCount": 2,
  "skippedCount": 1,
  "totalDurationMs": 15234
}
```

---

#### 4. `error` — 錯誤通知

發生不可恢復的錯誤時推送。

```
event: error
data: {"errorMessage":"GitHub API 速率限制已耗盡，請稍後再試"}
```

| 錯誤場景 | `errorMessage` 範例 |
|----------|---------------------|
| 請求驗證失敗 | `"請輸入 Markdown 內容"` |
| GitHub URL 格式不正確 | `"請輸入合法的 GitHub Repository URL"` |
| 私有 Repo | `"目前僅支援公開 Repository"` |
| GitHub API rate limit | `"GitHub API 速率限制已耗盡，請稍後再試"` |
| 伺服器內部錯誤 | `"系統發生錯誤，請稍後再試"` |

---

### HTTP 狀態碼

| 狀態碼 | 說明 |
|--------|------|
| `200` | 成功建立 SSE 串流（即使含有 Broken 連結） |
| `400` | 請求驗證失敗（JSON 格式錯誤、必填欄位缺失） |
| `429` | 速率限制（同一 IP 每分鐘超過 5 次）+ 回傳 `{"message": "請求過於頻繁，請稍後再試", "retryAfter": 60}` |

---

### 速率限制

- **限制規則**：同一來源 IP 每分鐘最多 5 次檢測請求（FR-036）
- **限制範圍**：僅套用於 `/api/check/sse` 端點
- **超過限制行為**：回傳 HTTP 429 + JSON 錯誤訊息

---

## 客戶端整合範例

### JavaScript（Fetch API + ReadableStream）

```javascript
async function startCheck(payload) {
    const response = await fetch('/api/check/sse', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    });
    
    if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || '檢測失敗');
    }
    
    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    
    while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        
        buffer += decoder.decode(value, { stream: true });
        const events = parseSSE(buffer);
        buffer = events.remaining;
        
        for (const event of events.parsed) {
            switch (event.type) {
                case 'progress':
                    onProgress(event.data);
                    break;
                case 'file-result':
                    onFileResult(event.data);
                    break;
                case 'complete':
                    onComplete(event.data);
                    break;
                case 'error':
                    onError(event.data);
                    break;
            }
        }
    }
}
```

---

## 頁面合約

### Index 頁面（`/`）

首頁提供檢測入口表單與結果顯示區域。

**表單元素**：

| 元素 | 型別 | ID/Name | 說明 |
|------|------|---------|------|
| 模式選擇 | Radio button group | `mode` | `MarkdownSource` / `RepoUrl` |
| Markdown 輸入區 | `<textarea>` | `markdownContent` | 顯示於 MarkdownSource 模式 |
| Repo URL 輸入 | `<input type="url">` | `repoUrl` | 顯示於 RepoUrl 模式 |
| 分支名稱 | `<input type="text">` | `branch` | 選填，顯示於 RepoUrl 模式 |
| 開始檢測 | `<button>` | `startCheck` | 觸發檢測 |

**結果區域**：

| 元素 | ID | 說明 |
|------|----|------|
| 進度列 | `progressBar` | 顯示「已檢查 N / 共 M 個連結」 |
| 檔案結果容器 | `fileResults` | 依檔案分組的檢測結果（即時追加） |
| 報告摘要 | `reportSummary` | 最終統計與總耗時 |
| 複製按鈕 | `copyMarkdown` | 「複製為 Markdown」按鈕 |
| 錯誤訊息 | `errorMessage` | 友善錯誤提示 |
