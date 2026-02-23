# 資料模型：Markdown Link Check

**Feature Branch**: `001-markdown-link-check`  
**建立日期**: 2026-02-23  
**狀態**: 完成

---

## 列舉型別

### LinkType（連結類型）

表示從 Markdown 解析出的連結種類。

| 值 | 名稱 | 說明 |
|----|------|------|
| `0` | `ExternalUrl` | 外部 HTTP/HTTPS URL |
| `1` | `RelativePath` | Repo 內相對路徑（如 `./docs/setup.md`） |
| `2` | `Anchor` | 錨點連結（如 `#installation`） |
| `3` | `Email` | 郵件連結（`mailto:` 開頭） |
| `4` | `Image` | 圖片連結（`![alt](url)`） |

**驗證規則**：
- `ExternalUrl` 必須以 `http://` 或 `https://` 開頭
- `Email` 必須以 `mailto:` 開頭且符合基本 email 格式
- `Anchor` 必須以 `#` 開頭
- `RelativePath` 不以 `http`/`https`/`#`/`mailto:` 開頭

---

### LinkStatus（連結狀態）

表示連結驗證後的結果狀態。

| 值 | 名稱 | 顯示符號 | 說明 |
|----|------|----------|------|
| `0` | `Healthy` | ✅ | 連結有效（HTTP 2xx 或檔案存在） |
| `1` | `Broken` | ❌ | 連結失效（HTTP 4xx/5xx、檔案不存在、SSRF 阻擋） |
| `2` | `Warning` | ⚠️ | 需注意（逾時、HTTP 429、301 永久重導向、過多重導向） |
| `3` | `Skipped` | ⏭️ | 跳過（Markdown 原始碼模式下的相對路徑） |

---

### CheckMode（檢測模式）

表示使用者選擇的檢測方式。

| 值 | 名稱 | 說明 |
|----|------|------|
| `0` | `MarkdownSource` | Markdown 原始碼模式（直接貼上文字） |
| `1` | `RepoUrl` | GitHub Repo URL 模式 |

---

## 核心實體

### CheckRequest（檢測請求）

代表使用者發起的一次連結檢測操作。

| 屬性 | 型別 | 必填 | 驗證規則 | 說明 |
|------|------|------|----------|------|
| `Mode` | `CheckMode` | ✅ | 必須為有效的 `CheckMode` 值 | 檢測模式 |
| `MarkdownContent` | `string?` | 條件必填 | `Mode == MarkdownSource` 時必填；最大 100,000 字元（FR-007）；不可為空白 | Markdown 原始碼文字 |
| `RepoUrl` | `string?` | 條件必填 | `Mode == RepoUrl` 時必填；格式 `https://github.com/{owner}/{repo}`（FR-002, FR-003） | GitHub Repository URL |
| `Branch` | `string?` | 選填 | 未指定時使用 Repo 預設分支（FR-005） | 指定分支名稱 |
| `RequestedAt` | `DateTimeOffset` | ✅（系統設定） | 自動設定為當前時間 | 發起時間 |
| `SourceIp` | `string` | ✅（系統設定） | 從 `HttpContext.Connection.RemoteIpAddress` 取得 | 來源 IP 位址 |

**條件驗證**：
- 當 `Mode == MarkdownSource` 時，`MarkdownContent` 不可為 null 或空白
- 當 `Mode == RepoUrl` 時，`RepoUrl` 不可為 null 或空白
- `RepoUrl` 格式正規表達式：`^https://github\.com/[a-zA-Z0-9\-_.]+/[a-zA-Z0-9\-_.]+$`

---

### MarkdownFile（Markdown 檔案）

代表一個被掃描的 Markdown 檔案。

| 屬性 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `FileName` | `string` | ✅ | 檔案名稱（如 `README.md`） |
| `RelativePath` | `string` | ✅ | 相對路徑（如 `docs/setup.md`） |
| `Content` | `string` | ✅ | 檔案完整內容 |
| `Links` | `IReadOnlyList<Link>` | ✅ | 從此檔案解析出的所有連結 |

**來源**：
- Markdown 原始碼模式：單一檔案，`FileName` 為 `"input.md"`，`RelativePath` 為 `"input.md"`
- Repo URL 模式：從 GitHub API 取得的 `.md` 檔案（FR-008, FR-009）

**限制**：
- 單一 Repo 最多 500 個 `.md` 檔案（FR-009）
- 僅掃描 `.md` 副檔名（FR-010）

---

### Link（連結）

代表從 Markdown 中解析出的一個連結。

| 屬性 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `Type` | `LinkType` | ✅ | 連結類型 |
| `RawText` | `string` | ✅ | 原始 Markdown 文字（如 `[文字](url)`） |
| `TargetUrl` | `string` | ✅ | 目標 URL 或路徑 |
| `LineNumber` | `int` | ✅ | 所在行號（1-based） |
| `SourceFile` | `MarkdownFile` | ✅ | 所屬的 Markdown 檔案 |

**型別判定邏輯**：
1. `TargetUrl` 以 `mailto:` 開頭 → `Email`
2. `TargetUrl` 以 `#` 開頭 → `Anchor`
3. `TargetUrl` 以 `http://` 或 `https://` 開頭 → `ExternalUrl`（`IsImage` 為 true 時為 `Image`）
4. 其餘 → `RelativePath`

**解析來源**：
- `LinkInline`（Markdig AST）→ inline link、reference-style link、image link
- `AutolinkInline`（Markdig AST）→ auto-link
- 忽略 `FencedCodeBlock`、`CodeInline`、`HtmlBlock` 內的內容（FR-014, FR-015, FR-016）

**限制**：
- 單一檔案最多 1,000 個連結（FR-018）
- 單次檢測最多 5,000 個連結（FR-035）

---

### LinkResult（連結檢測結果）

代表一個連結的驗證結果。

| 屬性 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `Link` | `Link` | ✅ | 對應的原始連結 |
| `Status` | `LinkStatus` | ✅ | 驗證結果狀態 |
| `HttpStatusCode` | `int?` | 選填 | HTTP 回應狀態碼（僅外部 URL 適用） |
| `ErrorType` | `string?` | 選填 | 錯誤類型（如 `"timeout"`、`"ssrf_blocked"`、`"file_not_found"`） |
| `ErrorMessage` | `string?` | 選填 | 錯誤訊息（人類可讀） |
| `RedirectUrl` | `string?` | 選填 | 301 永久重導向的新 URL（FR-021a） |
| `AnchorSuggestion` | `string?` | 選填 | 錨點拼字建議（FR-030） |
| `Duration` | `TimeSpan` | ✅ | 驗證耗時 |

**狀態判定規則**：

| 條件 | 狀態 | 附加資訊 |
|------|------|----------|
| HTTP 2xx（非 301 重導向） | `Healthy` | — |
| HTTP 2xx + 經 302 暫時重導向 | `Healthy` | — |
| HTTP 2xx + 經 301 永久重導向 | `Warning` | `RedirectUrl` = 新 URL |
| HTTP 4xx | `Broken` | `HttpStatusCode`、`ErrorMessage` |
| HTTP 5xx | `Broken` | `HttpStatusCode`、`ErrorMessage` |
| 逾時（> 10s） | `Warning` | `ErrorType = "timeout"` |
| HTTP 429 | `Warning` | `ErrorType = "rate_limited"` |
| 重導向 > 5 次 | `Warning` | `ErrorType = "too_many_redirects"` |
| SSRF 阻擋 | `Broken` | `ErrorType = "ssrf_blocked"`、`ErrorMessage = "禁止存取私有位址"` |
| 檔案存在（相對路徑） | `Healthy` | — |
| 檔案不存在（相對路徑） | `Broken` | `ErrorType = "file_not_found"` |
| 錨點存在 | `Healthy` | — |
| 錨點不存在 | `Broken` | `ErrorType = "anchor_not_found"`、`AnchorSuggestion`（若有） |
| 缺少 Repo 上下文（相對路徑 in Markdown 模式） | `Skipped` | `ErrorMessage = "缺少 Repo 上下文，無法驗證"` |
| `mailto:` 格式合法 | `Healthy` | — |
| `mailto:` 格式不合法 | `Broken` | `ErrorType = "invalid_email"` |

---

### CheckReport（檢測報告）

代表一次檢測的最終輸出結果。

| 屬性 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `FileCount` | `int` | ✅ | 掃描的檔案數量 |
| `TotalLinkCount` | `int` | ✅ | 檢查的連結總數 |
| `HealthyCount` | `int` | ✅ | `Healthy` 狀態的連結數量 |
| `BrokenCount` | `int` | ✅ | `Broken` 狀態的連結數量 |
| `WarningCount` | `int` | ✅ | `Warning` 狀態的連結數量 |
| `SkippedCount` | `int` | ✅ | `Skipped` 狀態的連結數量 |
| `TotalDuration` | `TimeSpan` | ✅ | 檢測總耗時 |
| `FileResults` | `IReadOnlyList<FileCheckResult>` | ✅ | 依檔案分組的檢測結果 |

---

### FileCheckResult（檔案檢測結果）

代表單一檔案的檢測結果（報告中的分組單位）。

| 屬性 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `File` | `MarkdownFile` | ✅ | 對應的 Markdown 檔案 |
| `LinkResults` | `IReadOnlyList<LinkResult>` | ✅ | 該檔案中所有連結的檢測結果 |
| `BrokenCount` | `int` | ✅ | 該檔案中 `Broken` 數量 |
| `WarningCount` | `int` | ✅ | 該檔案中 `Warning` 數量 |
| `HealthyCount` | `int` | ✅ | 該檔案中 `Healthy` 數量 |
| `SkippedCount` | `int` | ✅ | 該檔案中 `Skipped` 數量 |

**排序規則（FR-041）**：
- `LinkResults` 排序：❌ Broken 最前 → ⚠️ Warning 次之 → ✅ Healthy 摘要放最後
- `FileResults` 排序：含 Broken 的檔案優先 → 含 Warning 的檔案次之 → 全部 Healthy 的檔案

---

## SSE 事件模型

### CheckProgress（檢測進度事件）

用於 SSE 串流推送的進度事件。

| 屬性 | 型別 | 必填 | 說明 |
|------|------|------|------|
| `EventType` | `string` | ✅ | 事件類型：`progress`、`file-result`、`complete`、`error` |
| `CheckedCount` | `int?` | 選填 | 已檢查的連結數量（`progress` 事件） |
| `TotalCount` | `int?` | 選填 | 連結總數（`progress` 事件） |
| `CurrentFile` | `string?` | 選填 | 正在檢查的檔案名稱（`progress` 事件） |
| `FileResult` | `FileCheckResult?` | 選填 | 已完成的檔案結果（`file-result` 事件） |
| `Report` | `CheckReport?` | 選填 | 最終報告（`complete` 事件） |
| `ErrorMessage` | `string?` | 選填 | 錯誤訊息（`error` 事件） |

---

## 服務介面

### IMarkdownParserService

負責解析 Markdown 內容並擷取連結與標題錨點。

```
ParseLinks(content: string, fileName: string) → IReadOnlyList<Link>
ExtractAnchors(content: string) → IReadOnlyList<string>
```

### ILinkValidatorService

負責驗證單一連結的有效性。

```
ValidateAsync(link: Link, repoFiles: IReadOnlySet<string>?, anchorsMap: IReadOnlyDictionary<string, IReadOnlyList<string>>?, cancellationToken: CancellationToken) → Task<LinkResult>
```

### IGitHubRepoService

負責與 GitHub API 互動，掃描 Repo 中的 Markdown 檔案。

```
ValidateRepoUrlAsync(repoUrl: string, cancellationToken: CancellationToken) → Task<(string Owner, string Repo)>
GetDefaultBranchAsync(owner: string, repo: string, cancellationToken: CancellationToken) → Task<string>
ListMarkdownFilesAsync(owner: string, repo: string, branch: string, cancellationToken: CancellationToken) → Task<IReadOnlyList<string>>
GetFileContentAsync(owner: string, repo: string, branch: string, filePath: string, cancellationToken: CancellationToken) → Task<string>
```

### ILinkCheckOrchestrator

整合所有服務的檢測協調器，回傳 SSE 串流。

```
ExecuteAsync(request: CheckRequest, cancellationToken: CancellationToken) → IAsyncEnumerable<CheckProgress>
```

### IAnchorSuggestionService

提供錨點拼字建議。

```
Suggest(invalidAnchor: string, validAnchors: IReadOnlyList<string>) → string?
```

### IReportGeneratorService

負責將檢測結果組裝為最終報告。

```
GenerateReport(files: IReadOnlyList<FileCheckResult>, duration: TimeSpan) → CheckReport
GenerateMarkdownReport(report: CheckReport) → string
```

---

## 實體關係圖

```
CheckRequest (1) ──── creates ───→ (1..*) MarkdownFile
MarkdownFile (1) ──── contains ──→ (0..*) Link
Link         (1) ──── produces ──→ (1)    LinkResult
MarkdownFile (1) ──── groups ────→ (1)    FileCheckResult
FileCheckResult (0..*) ← aggregated ── (1) CheckReport
```

**流程**：
1. `CheckRequest` → `ILinkCheckOrchestrator.ExecuteAsync()`
2. Markdown 原始碼模式：建立單一 `MarkdownFile`
3. Repo URL 模式：`IGitHubRepoService` 掃描取得多個 `MarkdownFile`
4. 每個 `MarkdownFile` → `IMarkdownParserService.ParseLinks()` → 多個 `Link`
5. 每個 `Link` → `ILinkValidatorService.ValidateAsync()` → `LinkResult`
6. 依檔案分組為 `FileCheckResult` → 彙總為 `CheckReport`
7. 過程中透過 `IAsyncEnumerable<CheckProgress>` 串流推送進度與結果

---

## 設定模型

### LinkCheckOptions（檢測設定）

對應 `appsettings.json` 中 `LinkCheck` 區段。

| 屬性 | 型別 | 預設值 | 說明 |
|------|------|--------|------|
| `MaxMarkdownLength` | `int` | `100000` | Markdown 原始碼最大字元數（FR-007） |
| `MaxFilesPerRepo` | `int` | `500` | 單一 Repo 最多掃描檔案數（FR-009） |
| `MaxLinksPerFile` | `int` | `1000` | 單一檔案最多解析連結數（FR-018） |
| `MaxLinksPerCheck` | `int` | `5000` | 單次檢測最多連結數（FR-035） |
| `HttpTimeoutSeconds` | `int` | `10` | HTTP 請求逾時秒數 |
| `MaxRedirects` | `int` | `5` | 最大重導向次數 |
| `MaxRetries` | `int` | `1` | 最大重試次數（逾時/5xx） |
| `GlobalConcurrency` | `int` | `20` | 全域最大並行 HTTP 請求數（FR-032） |
| `PerDomainConcurrency` | `int` | `3` | 同一網域最大並行請求數（FR-031） |
| `UserAgent` | `string` | `"MarkdownLinkCheck/1.0"` | HTTP User-Agent 標頭（FR-037） |
