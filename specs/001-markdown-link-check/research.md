# Research: Markdown Link Check

**Feature Branch**: `001-markdown-link-check`  
**Created**: 2026-02-23  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

---

## 1. Markdown Parsing in C# — 連結擷取與錨點解析

### Decision

採用 **Markdig** (`Markdig` NuGet 套件) 作為 Markdown 解析引擎，透過其 AST（Abstract Syntax Tree）遍歷方式擷取所有連結。

### Rationale

- Markdig 是 .NET 生態系中最成熟、效能最高且維護最活躍的 Markdown 解析函式庫（由 Alexandre Mutel 維護，GitHub 4k+ stars）
- 完整支援 CommonMark 規範，且提供超過 20 個可選擴充套件（table、task list、auto-link 等）
- 產生強型別 AST（`MarkdownDocument`），可透過 `MarkdownObject.Descendants<T>()` 方法高效遍歷
- 原生支援所有需求中的連結類型：inline link、reference-style link、auto-link、image link
- 解析器自動處理 fenced code block 與 inline code，在 AST 中以 `CodeBlock` / `FencedCodeBlock` / `CodeInline` 節點呈現，不會在這些節點內部產生 `LinkInline` 節點——亦即 code block 內的 URL 天然不會被當作連結擷取
- 高效能：以 streaming token 方式解析，記憶體配置低

### Alternatives Considered

| 函式庫 | 評估結果 |
|--------|----------|
| **Markdig** (chosen) | CommonMark 相容、強型別 AST、活躍維護、擴充性佳 |
| **MarkdownSharp** | 僅支援原始 Markdown 語法，不產生 AST，無法方便遍歷擷取連結，已停止維護 |
| **CommonMark.NET** | 支援 CommonMark 但維護頻率低，AST API 較不友善 |
| **手寫 Regex** | 無法正確處理巢狀語法、code block 排除、reference-style 連結解析，維護成本高 |

### Key Implementation Notes

**安裝：**
```
dotnet add package Markdig
```

**解析與連結擷取核心模式：**
```csharp
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

var pipeline = new MarkdownPipelineBuilder()
    .UseAutoLinks()        // 支援 <url> auto-link
    .UsePipeTables()       // 表格內連結
    .Build();

MarkdownDocument document = Markdown.Parse(markdownText, pipeline);

// 擷取所有 LinkInline（含 inline link、image link、auto-link）
var links = document.Descendants<LinkInline>();
foreach (var link in links)
{
    string url = link.Url ?? string.Empty;
    bool isImage = link.IsImage;
    int lineNumber = link.Line + 1;  // Line 是 0-based
    // link.Label 用於 reference-style
}

// 擷取所有 AutolinkInline（<url> 語法）
var autoLinks = document.Descendants<AutolinkInline>();
foreach (var autoLink in autoLinks)
{
    string url = autoLink.Url;
    bool isEmail = autoLink.IsEmail;
}
```

**HTML 註解排除：**
- Markdig 將 HTML 註解解析為 `HtmlBlock`（區塊級）或 `HtmlInline`（行內級）節點
- 解析已完成的 AST 中，HTML 註解內部的文字不會產生 `LinkInline` 節點
- 額外安全措施：遍歷 AST 時，可檢查 `LinkInline` 的 parent 是否為 `HtmlBlock`，若是則跳過
- 對於 `<!-- [link](url) -->` 這類情境，Markdig 會整體視為 HTML block，不解析內部 Markdown 語法

**Reference-style 連結：**
- Markdig 在解析階段自動解析 reference-style 連結（`[text][ref]` + `[ref]: url`），在 AST 中以 `LinkInline` 呈現，其 `Url` 屬性已是解析後的實際 URL
- 不需額外處理 reference definition，Markdig 已自動匹配

**標題錨點擷取（供錨點驗證使用）：**
```csharp
var headings = document.Descendants<HeadingBlock>();
foreach (var heading in headings)
{
    // 取得標題純文字
    string headingText = heading.Inline?.FirstChild?.ToString() ?? string.Empty;
    
    // GitHub 風格錨點轉換規則：
    // 1. 轉小寫
    // 2. 移除非字母數字字元（保留連字號與空格）
    // 3. 空格替換為連字號
    // 4. 移除前後連字號
    string anchor = GenerateGitHubAnchor(headingText);
}
```

- GitHub 錨點產生規則需自行實作，核心邏輯：`ToLowerInvariant()` → 移除特殊字元 → 空格轉 `-` → 移除連續 `-`
- 重複標題的處理：同名標題第二次出現時附加 `-1`、`-2` 等後綴（如 `#installation`、`#installation-1`）

**Code Block / Inline Code 排除機制：**
- Markdig 的 AST 結構天然保證 `FencedCodeBlock`、`CodeBlock`、`CodeInline` 內部不會產生 `LinkInline` 子節點
- 因此只要使用 `document.Descendants<LinkInline>()` 遍歷，自動排除 code block 內容
- 無需額外過濾邏輯

**效能數據：**
- 100KB Markdown 文件解析耗時約 1-2ms
- 記憶體配置低，適合 server-side 使用
- 對於 100,000 字元上限（FR-007）綽綽有餘

---

## 2. Server-Sent Events (SSE) in ASP.NET Core 10.0

### Decision

使用 ASP.NET Core **Minimal API** 搭配 `IResult` 自訂實作建立 SSE 端點；客戶端使用瀏覽器原生 **EventSource API** 搭配 jQuery 更新 UI。

### Rationale

- SSE 是單向伺服器推送的最佳選擇，比 WebSocket 輕量、比 Long Polling 高效
- 瀏覽器原生支援 `EventSource` API，無需額外函式庫
- ASP.NET Core Minimal API 提供簡潔的路由定義方式，適合單一 SSE 端點
- 專案已規劃使用 Minimal API（`Endpoints/LinkCheckSseEndpoint.cs`），與 Razor Pages 共存
- ASP.NET Core 10.0 原生支援 streaming response，與 `HttpContext.Response` 搭配良好

### Alternatives Considered

| 方案 | 評估結果 |
|------|----------|
| **SSE + Minimal API** (chosen) | 輕量、單向推送符合需求、原生瀏覽器支援、實作簡潔 |
| **WebSocket (SignalR)** | 雙向通訊過度設計、額外相依套件、本專案不需客戶端→伺服器即時通訊 |
| **Long Polling** | 延遲較高、連線開銷大、實作複雜度高 |
| **gRPC Server Streaming** | 瀏覽器不原生支援、需 gRPC-Web 代理、複雜度高 |

### Key Implementation Notes

**伺服器端 — Minimal API SSE 端點：**
```csharp
// Endpoints/LinkCheckSseEndpoint.cs
public static class LinkCheckSseEndpoint
{
    public static void MapLinkCheckSse(this WebApplication app)
    {
        app.MapPost("/api/check/sse", async (
            HttpContext context,
            CheckRequest request,
            ILinkCheckOrchestrator orchestrator,
            CancellationToken cancellationToken) =>
        {
            // 設定 SSE 必要 headers
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";

            await foreach (var progress in orchestrator
                .ExecuteAsync(request, cancellationToken))
            {
                // SSE 格式：event: <type>\ndata: <json>\n\n
                await context.Response.WriteAsync(
                    $"event: {progress.EventType}\n", cancellationToken);
                await context.Response.WriteAsync(
                    $"data: {JsonSerializer.Serialize(progress)}\n\n",
                    cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }

            // 發送完成事件
            await context.Response.WriteAsync(
                "event: complete\ndata: {}\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        });
    }
}
```

**SSE 協議格式要求：**
- Content-Type 必須為 `text/event-stream`
- 每則訊息格式：`event: <type>\ndata: <json>\n\n`（兩個換行結尾）
- `data:` 欄位可多行，每行前綴 `data:`
- 可選 `id:` 欄位供斷線重連用（本專案不需要）
- 可選 `retry:` 欄位指定重連間隔

**事件類型設計：**
- `progress` — 進度更新（已檢查 N / 共 M 個連結）
- `file-result` — 單一檔案檢測結果（FR-039 即時串流）
- `complete` — 檢測全部完成
- `error` — 錯誤通知

**使用 `IAsyncEnumerable<T>` 作為串流源：**
- `ILinkCheckOrchestrator.ExecuteAsync()` 回傳 `IAsyncEnumerable<CheckProgress>`
- 自然搭配 `await foreach` 逐筆推送
- `CancellationToken` 偵測客戶端斷線，自動停止處理

**客戶端 — EventSource API + jQuery：**
```javascript
// wwwroot/js/site.js
function startCheck(formData) {
    // 注意：EventSource 僅支援 GET，故需搭配 fetch + ReadableStream
    // 或改用 POST + fetch streaming
    fetch('/api/check/sse', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(formData)
    }).then(response => {
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        
        function read() {
            reader.read().then(({ done, value }) => {
                if (done) return;
                const text = decoder.decode(value);
                // 解析 SSE 格式
                parseSSE(text).forEach(event => {
                    switch (event.type) {
                        case 'progress':
                            updateProgress(event.data);
                            break;
                        case 'file-result':
                            appendFileResult(event.data);
                            break;
                        case 'complete':
                            showComplete(event.data);
                            break;
                        case 'error':
                            showError(event.data);
                            break;
                    }
                });
                read();
            });
        }
        read();
    });
}
```

**重要：EventSource 與 POST 的限制**
- 瀏覽器原生 `EventSource` API 僅支援 GET 請求
- 本專案需 POST 傳送 Markdown 內容（可能很大），因此需改用 **Fetch API + ReadableStream** 解析 SSE 格式
- 或者：POST 先建立 check session（回傳 session ID），再用 GET EventSource 訂閱該 session 的結果
- 建議方案：**Fetch Streaming**（直接 POST + 讀取 streaming response），避免額外 session 管理

**連線管理與錯誤處理：**
- 伺服器端透過 `CancellationToken`（`HttpContext.RequestAborted`）偵測客戶端斷線
- 客戶端斷線後，伺服器端的 `WriteAsync` 會拋出 `OperationCanceledException`，應 catch 後優雅結束
- 客戶端設定 `AbortController` 以支援使用者手動取消
- ASP.NET Core 預設的 Kestrel 不會 buffer SSE response（`text/event-stream` 會被自動禁用 buffering）

**ASP.NET Core 10.0 特定備註：**
- .NET 10  繼續改善 Minimal API 的 `IResult` 模式，但 SSE 仍需手動寫入 `HttpContext.Response`
- 考量使用 `Results.Stream()` helper 簡化實作（若 .NET 10 提供改善版）
- Kestrel HTTP/2 支援 SSE，但 HTTP/1.1 更普遍且相容性更好
- 確保 `ResponseBuffering` middleware 不要對 SSE 端點生效

---

## 3. IHttpClientFactory Patterns for Link Validation

### Decision

使用 **Named HttpClient**（名稱 `"LinkChecker"`）搭配 `IHttpClientFactory`，以 `SocketsHttpHandler` callback 實作 SSRF 防護，使用 `SemaphoreSlim` 實作並行控制。

### Rationale

- `IHttpClientFactory` 是 ASP.NET Core 官方推薦的 HttpClient 管理方式，自動管理 `HttpMessageHandler` 生命週期、避免 socket exhaustion
- Named HttpClient 足夠本專案使用（僅一種用途），比 Typed HttpClient 更簡潔
- `SocketsHttpHandler.ConnectCallback` 提供 DNS 解析後、TCP 連線前的攔截點，是 SSRF 防護的最佳位置
- `SemaphoreSlim` 相比 Polly Bulkhead 更輕量、無外部相依

### Alternatives Considered

| 方案 | 評估結果 |
|------|----------|
| **Named HttpClient + SemaphoreSlim** (chosen) | 簡潔、無額外相依、控制力強 |
| **Typed HttpClient** | 增加一個 class 的複雜度，本專案只有一種 HTTP 呼叫場景，不需要 |
| **Polly Bulkhead** | 需額外安裝 `Microsoft.Extensions.Http.Polly` 或 `Microsoft.Extensions.Http.Resilience`，對 per-domain 限制不夠直覺 |
| **自行 `new HttpClient()`** | 無法管理 handler 生命週期、會導致 socket exhaustion |

### Key Implementation Notes

**DI 註冊 — Named HttpClient：**
```csharp
// Program.cs
builder.Services.AddHttpClient("LinkChecker", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);  // FR: 10 秒逾時
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "MarkdownLinkCheck/1.0 (+https://github.com/user/repo)"); // FR-037
    client.MaxResponseContentBufferSize = 1024;  // 僅需讀取 headers
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    MaxAutomaticRedirections = 5,    // FR: 最多 5 次重導向
    AllowAutoRedirect = true,
    ConnectCallback = SsrfProtectionCallback  // SSRF 防護
});
```

**SSRF 防護 — SocketsHttpHandler.ConnectCallback：**
```csharp
private static async ValueTask<Stream> SsrfProtectionCallback(
    SocketsHttpConnectionContext context,
    CancellationToken cancellationToken)
{
    // DNS 解析
    var entry = await Dns.GetHostEntryAsync(
        context.DnsEndPoint.Host, cancellationToken);

    // 檢查所有解析到的 IP 是否為私有位址
    foreach (var address in entry.AddressList)
    {
        if (IsPrivateIpAddress(address))
        {
            throw new HttpRequestException(
                $"禁止存取私有位址: {address}");
        }
    }

    // 通過檢查後建立正常 TCP 連線
    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    try
    {
        await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
        return new NetworkStream(socket, ownsSocket: true);
    }
    catch
    {
        socket.Dispose();
        throw;
    }
}

private static bool IsPrivateIpAddress(IPAddress address)
{
    byte[] bytes = address.GetAddressBytes();
    return address.IsIPv6LinkLocal
        || address.IsIPv6SiteLocal
        || IPAddress.IsLoopback(address)               // 127.0.0.0/8
        || (bytes[0] == 10)                             // 10.0.0.0/8
        || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) // 172.16.0.0/12
        || (bytes[0] == 192 && bytes[1] == 168);        // 192.168.0.0/16
}
```

**HEAD → GET 回退：**
```csharp
public async Task<LinkResult> ValidateUrlAsync(string url, CancellationToken ct)
{
    var client = httpClientFactory.CreateClient("LinkChecker");

    using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
    var response = await client.SendAsync(headRequest, 
        HttpCompletionOption.ResponseHeadersRead, ct);

    if (response.StatusCode == HttpStatusCode.MethodNotAllowed)  // 405
    {
        // HEAD 被拒絕，改用 GET
        using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
        response = await client.SendAsync(getRequest,
            HttpCompletionOption.ResponseHeadersRead, ct);
    }

    return MapToLinkResult(response);
}
```

**重試策略（1 次重試 on timeout / 5xx）：**
```csharp
// 簡潔方式：在 service 層實作
private async Task<HttpResponseMessage> SendWithRetryAsync(
    HttpClient client, HttpRequestMessage request, CancellationToken ct)
{
    try
    {
        var response = await client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, ct);

        if ((int)response.StatusCode >= 500)
        {
            // 5xx 重試一次
            using var retryRequest = CloneRequest(request);
            return await client.SendAsync(retryRequest,
                HttpCompletionOption.ResponseHeadersRead, ct);
        }
        return response;
    }
    catch (TaskCanceledException) when (!ct.IsCancellationRequested)
    {
        // Timeout — 重試一次
        using var retryRequest = CloneRequest(request);
        return await client.SendAsync(retryRequest,
            HttpCompletionOption.ResponseHeadersRead, ct);
    }
}
```

**並行控制 — Per-Domain + Global：**
```csharp
public class ConcurrencyLimiter
{
    private readonly SemaphoreSlim _globalSemaphore = new(20, 20);  // FR-032
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _domainSemaphores = new();

    public async Task<T> ExecuteAsync<T>(
        Uri uri, Func<Task<T>> action, CancellationToken ct)
    {
        var domainSemaphore = _domainSemaphores.GetOrAdd(
            uri.Host, _ => new SemaphoreSlim(3, 3));  // FR-031

        await _globalSemaphore.WaitAsync(ct);
        try
        {
            await domainSemaphore.WaitAsync(ct);
            try
            {
                return await action();
            }
            finally
            {
                domainSemaphore.Release();
            }
        }
        finally
        {
            _globalSemaphore.Release();
        }
    }
}
```

**URL 去重（FR-028）：**
```csharp
private readonly ConcurrentDictionary<string, Task<LinkResult>> _urlCache = new();

public Task<LinkResult> ValidateUrlAsync(string url, CancellationToken ct)
{
    return _urlCache.GetOrAdd(url, key => ValidateUrlInternalAsync(key, ct));
}
```
- 使用 `ConcurrentDictionary<string, Task<LinkResult>>` 確保同一 URL 只發送一次 HTTP 請求
- 後續相同 URL 直接 await 同一個 Task
- 注意：需規範化 URL（移除 trailing slash、統一 scheme casing）以提高去重命中率

**301 永久重導向偵測（FR-021a）：**
- `AllowAutoRedirect = true` 會自動跟隨重導向，但會遺失中間重導向資訊
- 解法一：設定 `AllowAutoRedirect = false`，手動追蹤重導向鏈
- 解法二：使用 `DelegatingHandler` 記錄重導向歷程
- 建議方案：`AllowAutoRedirect = false` + 手動重導向迴圈，可精確區分 301 vs 302

---

## 4. GitHub REST API for Repo Scanning (Unauthenticated)

### Decision

使用 **GitHub REST API v3**（無需認證），透過 **Git Trees API** 列出所有 `.md` 檔案，透過 **Raw Content URL** 取得檔案內容。

### Rationale

- Git Trees API 可一次取得整個 repository 的檔案樹，避免遞迴呼叫目錄 API
- Raw Content URL 不消耗 API rate limit（走 CDN）
- 無需 GitHub SDK（`Octokit`），直接用 `HttpClient` 呼叫 REST API，簡化相依

### Alternatives Considered

| 方案 | 評估結果 |
|------|----------|
| **REST API + HttpClient** (chosen) | 輕量、無額外相依、控制力強 |
| **Octokit.NET** | 完整 SDK 但相依較重，本專案只需 3-4 個 API 呼叫 |
| **GraphQL API v4** | 查詢彈性高但需認證 Token，不符合「免認證」需求 |
| **Git Clone (LibGit2Sharp)** | 下載整個 repo 耗時耗空間，不適合 web 服務 |

### Key Implementation Notes

**步驟 1 — 取得預設分支：**
```
GET https://api.github.com/repos/{owner}/{repo}
```
- 回應中 `default_branch` 欄位為預設分支名稱
- 若使用者指定分支，跳過此步驟

**步驟 2 — 取得 Branch 對應的 commit SHA：**
```
GET https://api.github.com/repos/{owner}/{repo}/branches/{branch}
```
- 回應中 `commit.sha` 為該分支最新 commit SHA
- 或直接使用 Trees API 搭配分支名稱

**步驟 3 — 取得完整檔案樹（Git Trees API）：**
```
GET https://api.github.com/repos/{owner}/{repo}/git/trees/{branch_sha}?recursive=1
```
- `?recursive=1` 遞迴展開所有子目錄
- 回應中 `tree` 陣列包含所有檔案，篩選 `path` 以 `.md` 結尾者
- 每個項目包含 `path`、`sha`、`size`、`type`（`blob` = 檔案）
- 如果 tree 超過限制（`truncated: true`），需改用逐層 API 呼叫

**步驟 4 — 取得檔案原始內容：**
```
GET https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}
```
- 此 URL 不經過 API rate limit（走 GitHub CDN）
- 直接取得檔案純文字內容
- 適合大量檔案下載

**Rate Limit 處理（FR-033）：**
- 未認證 API rate limit：**60 requests/hour**（per IP）
- 回應 headers 包含：
  - `X-RateLimit-Limit`: 60
  - `X-RateLimit-Remaining`: 剩餘次數
  - `X-RateLimit-Reset`: Unix timestamp（重置時間）
- 策略：
  1. 每次 API 回應後檢查 `X-RateLimit-Remaining`
  2. 若 `Remaining <= 5`，提前發出警告
  3. 若收到 HTTP 403 + `X-RateLimit-Remaining: 0`，顯示友善提示
  4. 計算 `Reset` 時間告知使用者需等待多久

**API 呼叫摘要（典型 Repo 掃描一次使用量）：**
| API 呼叫 | 用途 | 次數 | 消耗 rate limit |
|----------|------|------|-----------------|
| `GET /repos/{owner}/{repo}` | 預設分支 | 1 | 1 |
| `GET /repos/{owner}/{repo}/git/trees/{sha}?recursive=1` | 檔案清單 | 1 | 1 |
| `GET raw.githubusercontent.com/...` | 檔案內容 | N | 0（CDN） |
| **合計** | | | **2** |

- 每次 Repo 掃描僅消耗 2 次 API rate limit（假設不需額外 branch 查詢）
- 60 req/hour 足以支援約 30 次 Repo 掃描

**錯誤處理：**
- HTTP 404：Repo 不存在或為私有 → 顯示「請確認 URL 正確且為公開 Repository」
- HTTP 403 + rate limit 耗盡 → 顯示友善提示含重置時間
- `truncated: true`（超大 repo）→ 提示使用者 repo 過大

**Named HttpClient 建議：**
- 建立獨立的 Named HttpClient `"GitHub"` 用於 GitHub API 呼叫
- 設定 `User-Agent` header（GitHub API 強制要求）
- 設定 `Accept: application/vnd.github.v3+json`

---

## 5. Levenshtein Distance / Edit Distance in C#

### Decision

自行實作 **Levenshtein Distance** 演算法（Wagner-Fischer 動態規劃法），使用 `Span<int>` 優化記憶體配置。

### Rationale

- .NET 沒有內建 Levenshtein Distance 函式
- 演算法實作簡潔（約 20 行），無需引入外部函式庫
- 使用 `Span<int>` 可避免 heap allocation、stack 上分配陣列
- 錨點比對場景規模小（一個文件通常 10-50 個標題），效能完全不是瓶頸

### Alternatives Considered

| 方案 | 評估結果 |
|------|----------|
| **自行實作 + Span 優化** (chosen) | 零相依、20 行程式碼、效能足夠、完全掌控 |
| **FuzzySharp NuGet** | 提供多種模糊比對演算法，但引入外部相依過度，本專案只需基本編輯距離 |
| **Fastenshtein NuGet** | 高效能 Levenshtein 實作，但又增加一個相依套件，收益不大 |
| **`System.Globalization.StringInfo`** | 不提供編輯距離功能 |

### Key Implementation Notes

**Wagner-Fischer 演算法（兩行優化版）：**
```csharp
public static int ComputeLevenshteinDistance(
    ReadOnlySpan<char> source, ReadOnlySpan<char> target)
{
    int sourceLength = source.Length;
    int targetLength = target.Length;

    if (sourceLength == 0) return targetLength;
    if (targetLength == 0) return sourceLength;

    // 使用兩個一維陣列代替二維矩陣
    Span<int> previousRow = stackalloc int[targetLength + 1];
    Span<int> currentRow = stackalloc int[targetLength + 1];

    for (int j = 0; j <= targetLength; j++)
        previousRow[j] = j;

    for (int i = 1; i <= sourceLength; i++)
    {
        currentRow[0] = i;
        for (int j = 1; j <= targetLength; j++)
        {
            int cost = source[i - 1] == target[j - 1] ? 0 : 1;
            currentRow[j] = Math.Min(
                Math.Min(
                    previousRow[j] + 1,      // deletion
                    currentRow[j - 1] + 1),   // insertion
                previousRow[j - 1] + cost);   // substitution
        }

        // swap rows
        (previousRow, currentRow) = (currentRow, previousRow);
    }

    return previousRow[targetLength];
}
```

**錨點建議使用模式（FR-030）：**
```csharp
public string? SuggestAnchor(string invalidAnchor, IReadOnlyList<string> validAnchors)
{
    string? bestMatch = null;
    int bestDistance = int.MaxValue;

    foreach (var anchor in validAnchors)
    {
        // 早期剪枝：長度差異超過閾值就跳過
        if (Math.Abs(invalidAnchor.Length - anchor.Length) > 2)
            continue;

        int distance = ComputeLevenshteinDistance(
            invalidAnchor.AsSpan(), anchor.AsSpan());

        if (distance <= 2 && distance < bestDistance)
        {
            bestDistance = distance;
            bestMatch = anchor;
        }
    }

    return bestMatch;
}
```

**效能考量：**
- 典型場景：一個 Markdown 文件有 10-50 個標題，failed anchor 需比對 10-50 次
- 每次比對時間複雜度 $O(m \times n)$，其中 $m$, $n$ 為字串長度（通常 < 50 字元）
- `stackalloc` 完全避免 GC 壓力
- 總耗時微秒級，完全不是效能瓶頸
- 早期剪枝（長度差異 > 閾值即跳過）可進一步減少計算量

---

## 6. Rate Limiting in ASP.NET Core 10.0

### Decision

使用 ASP.NET Core 內建的 **`Microsoft.AspNetCore.RateLimiting`** middleware，以 **Fixed Window** 策略實作每 IP 每分鐘 5 次請求限制。

### Rationale

- ASP.NET Core 7.0+ 內建速率限制 middleware，無需外部函式庫
- Fixed Window 策略最符合「每分鐘 5 次」的需求描述
- 內建支援 per-IP 分割（`PartitionedRateLimiter`）
- 提供 `OnRejected` callback 自訂拒絕回應

### Alternatives Considered

| 方案 | 評估結果 |
|------|----------|
| **Built-in `RateLimiting` middleware** (chosen) | 原生內建、設定簡潔、支援 per-IP 分割 |
| **AspNetCoreRateLimit NuGet** | 功能豐富但 ASP.NET Core 已內建，不需額外相依 |
| **自行實作** | 重新造輪，已有官方方案 |
| **Reverse Proxy (Nginx/YARP)** | 部署複雜度高，不適合單一應用情境 |

### Key Implementation Notes

**DI 註冊與 Middleware 設定：**
```csharp
// Program.cs
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "請求過於頻繁，請稍後再試",
            retryAfter = context.Lease.TryGetMetadata(
                MetadataName.RetryAfter, out var retryAfter)
                ? retryAfter.TotalSeconds
                : 60
        }, cancellationToken);
    };

    options.AddFixedWindowLimiter("CheckEndpoint", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;             // FR-036: 每分鐘 5 次
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;              // 不排隊，直接拒絕
    });

    // 全域 per-IP 策略
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context =>
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                remoteIp,
                _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 5,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });
});

// Middleware pipeline（放在 UseRouting 之後）
app.UseRateLimiter();
```

**SSE 端點套用速率限制：**
```csharp
app.MapPost("/api/check/sse", handler)
   .RequireRateLimiting("CheckEndpoint");
```

**注意事項：**
- `GlobalLimiter` 與 endpoint-specific limiter 會同時生效（取較嚴格者）
- 建議只用一種：Global per-IP 或 endpoint-specific，避免混淆
- `RemoteIpAddress` 在 reverse proxy 背後可能都是 proxy IP → 需配合 `ForwardedHeaders` middleware
- Rate limit headers（`Retry-After`、`X-RateLimit-*`）需自行在 `OnRejected` 中回傳

**Forward Headers 設定（若有 reverse proxy）：**
```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor 
                             | ForwardedHeaders.XForwardedProto;
});
app.UseForwardedHeaders();  // 必須在 UseRateLimiter 之前
```

---

## 7. Serilog Configuration for ASP.NET Core 10.0

### Decision

使用 **Serilog** 搭配 **Console sink** 與 **File sink**，透過 `appsettings.json` 設定，以結構化記錄連結檢測事件。

### Rationale

- Serilog 是 .NET 生態系中最廣泛使用的結構化 logging 函式庫
- 支援 `appsettings.json` 設定（透過 `Serilog.Settings.Configuration`），無需程式碼修改即可調整日誌行為
- 豐富的 enricher 生態系（RequestId、SourceContext、Environment 等）
- 專案已指定使用 Serilog

### Alternatives Considered

| 方案 | 評估結果 |
|------|----------|
| **Serilog** (chosen) | 生態系最豐富、結構化記錄、`appsettings.json` 設定 |
| **NLog** | 功能類似但生態系較小、結構化記錄支援稍弱 |
| **Built-in `ILogger`** | 缺乏進階 sink、enricher、結構化查詢能力 |
| **OpenTelemetry Logging** | 適合分散式追蹤場景，本專案規模不需要 |

### Key Implementation Notes

**NuGet 套件：**
```
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Settings.Configuration
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.Thread
```

**Program.cs 整合：**
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

try
{
    builder.Host.UseSerilog();

    // ... app 設定 ...

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

**appsettings.json 設定：**
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.AspNetCore.Hosting": "Information",
        "System.Net.Http": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

**連結檢測事件的結構化記錄模式：**
```csharp
// 檢測開始
_logger.LogInformation("Link check started. Mode={Mode}, InputLength={InputLength}",
    request.Mode, request.InputLength);

// 單一連結驗證結果
_logger.LogInformation(
    "Link validated: {Url} Status={Status} HttpCode={HttpStatusCode} Duration={Duration}ms",
    link.Url, result.Status, result.HttpStatusCode, result.Duration.TotalMilliseconds);

// 檢測完成
_logger.LogInformation(
    "Link check completed. Files={FileCount}, Links={LinkCount}, " +
    "Healthy={HealthyCount}, Broken={BrokenCount}, Warning={WarningCount}, " +
    "Duration={TotalDuration}ms",
    report.FileCount, report.LinkCount,
    report.HealthyCount, report.BrokenCount, report.WarningCount,
    report.TotalDuration.TotalMilliseconds);

// SSRF 攔截
_logger.LogWarning("SSRF blocked: {Url} resolved to private IP {IpAddress}",
    url, ipAddress);

// Rate limit
_logger.LogWarning("GitHub API rate limit low: {Remaining}/{Limit}, resets at {ResetTime}",
    remaining, limit, resetTime);
```

**日誌層級指引：**
| 層級 | 用途 |
|------|------|
| `Debug` | 解析細節、AST 遍歷步驟 |
| `Information` | 檢測開始/完成、單一連結結果、進度更新 |
| `Warning` | SSRF 攔截、rate limit 接近、HEAD→GET 回退、301 重導向 |
| `Error` | HTTP 請求失敗、GitHub API 錯誤 |
| `Fatal` | 應用程式啟動失敗 |

**開發環境差異（appsettings.Development.json）：**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.AspNetCore": "Information"
      }
    }
  }
}
```

---

## Summary of Decisions

| 主題 | 決策 | 關鍵理由 |
|------|------|----------|
| Markdown 解析 | Markdig | CommonMark 相容、強型別 AST、天然排除 code block |
| SSE 實作 | Minimal API + Fetch Streaming | 輕量、單向推送、原生瀏覽器支援 |
| HTTP Client | Named HttpClient `"LinkChecker"` + `SocketsHttpHandler` | 官方推薦、SSRF 防護最佳攔截點 |
| GitHub API | REST v3 Git Trees API + Raw Content | 最少 API 呼叫、CDN 不消耗 rate limit |
| 編輯距離 | 自行實作 Levenshtein + `Span<int>` | 零相依、20 行程式碼、效能足夠 |
| Rate Limiting | `Microsoft.AspNetCore.RateLimiting` Fixed Window | 內建 middleware、per-IP 分割 |
| Logging | Serilog + Console/File sinks | 結構化記錄、`appsettings.json` 設定 |

---

## NuGet Packages Summary

| 套件 | 用途 | 版本建議 |
|------|------|----------|
| `Markdig` | Markdown 解析與 AST 遍歷 | Latest stable |
| `Serilog.AspNetCore` | Serilog 整合 ASP.NET Core | Latest stable |
| `Serilog.Settings.Configuration` | appsettings.json 設定 Serilog | Latest stable |
| `Serilog.Sinks.Console` | Console 輸出 | Latest stable |
| `Serilog.Sinks.File` | File 輸出（rolling） | Latest stable |
| `Serilog.Enrichers.Environment` | Machine name enricher | Latest stable |
| `Serilog.Enrichers.Thread` | Thread ID enricher | Latest stable |
| `xunit` | 單元測試框架 | Latest stable |
| `Moq` | Mocking 框架 | Latest stable |
| `Microsoft.AspNetCore.Mvc.Testing` | WebApplicationFactory 整合測試 | Latest stable (.NET 10) |

> 注意：`Microsoft.AspNetCore.RateLimiting` 已內建於 ASP.NET Core 10.0，無需額外安裝。
