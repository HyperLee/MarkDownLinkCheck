<div align="center">

# 🔗 Markdown Link Check

**一鍵檢測 Markdown 文件中的失效連結，降低文件維護成本**

[![.NET](https://img.shields.io/badge/.NET-10.0-512bd4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120?style=flat-square&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![License](https://img.shields.io/badge/License-MIT-blue?style=flat-square)](LICENSE)

[功能特色](#功能特色) • [快速開始](#快速開始) • [使用方式](#使用方式) • [技術架構](#技術架構) • [開發指南](#開發指南)

</div>

---

Markdown Link Check 是一個 Web 應用程式，能自動掃描 Markdown 文件中的所有連結，並即時驗證其有效性。無論是直接貼上 Markdown 原始碼，或輸入公開的 GitHub Repository URL，都能快速產出完整的檢測報告，幫助開源專案維護者及技術文件作者有效管理連結品質。

## 功能特色

- **雙模式檢測** — 支援「Markdown 原始碼」直接貼上與「GitHub Repo URL」兩種輸入模式
- **即時串流結果** — 透過 Server-Sent Events (SSE) 即時推送檢測進度，不需等全部完成
- **完整連結解析** — 支援 inline link、reference-style link、autolink、圖片連結等 Markdown 語法
- **智慧狀態分類** — 將連結分為 ✅ Healthy、❌ Broken、⚠️ Warning、⏭️ Skipped 四種狀態
- **錨點拼字建議** — 當錨點連結失效時，自動建議編輯距離最近的替代錨點（did you mean #xxx?）
- **安全防護** — 內建 SSRF 防護（阻擋私有 IP）、IP 速率限制、並行請求控制
- **一鍵匯出** — 檢測完成後可一鍵複製 Markdown 格式報告，方便貼入 Issue 或 PR

## 快速開始

### 環境需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/downloads)

### 安裝與執行

```bash
# 複製專案
git clone https://github.com/HyperLee/MarkDownLinkCheck.git
cd MarkDownLinkCheck

# 建構並執行
dotnet run --project MarkDownLinkCheck
```

應用程式啟動後，開啟瀏覽器前往 `http://localhost:5184` 即可使用。

## 使用方式

### Markdown 原始碼模式

1. 選擇「Markdown 原始碼」模式
2. 將 Markdown 內容貼入文字輸入區（上限 100,000 字元）
3. 點選「開始檢測」

系統會解析所有連結並即時顯示檢測進度，完成後產出分類報告。

### GitHub Repo URL 模式

1. 選擇「GitHub Repository URL」模式
2. 輸入公開 Repo 的 URL（格式：`https://github.com/{owner}/{repo}`）
3. 可選擇指定分支名稱
4. 點選「開始檢測」

系統將自動掃描 Repo 中所有 `.md` 檔案（上限 500 個），逐檔檢測連結並串流結果。

> [!NOTE]
> 目前僅支援公開（Public）Repository，未來版本將評估支援私有 Repo。

### 檢測報告

檢測完成後，報告會依檔案分組，每個連結標示行號與狀態：

| 狀態 | 說明 |
|------|------|
| ✅ Healthy | 連結有效（HTTP 2xx 或檔案存在） |
| ❌ Broken | 連結失效（HTTP 4xx/5xx、檔案不存在、SSRF 阻擋） |
| ⚠️ Warning | 需要注意（逾時、HTTP 429、301 永久重導向、過多重導向） |
| ⏭️ Skipped | 跳過檢測（原始碼模式下的相對路徑、Code Block 內的連結） |

點選「複製為 Markdown」按鈕可將報告複製到剪貼簿。

## 技術架構

```
┌─────────────────────────────────────────────────┐
│                   Browser (SSE Client)          │
│              Index.cshtml + site.js             │
└──────────────────────┬──────────────────────────┘
                       │ POST /api/check/sse
                       ▼
┌─────────────────────────────────────────────────┐
│              ASP.NET Core 10 Server             │
│                                                 │
│  ┌─────────────┐  ┌──────────────────────────┐  │
│  │ SSE Endpoint │→ │  LinkCheckOrchestrator   │  │
│  └─────────────┘  └──────────┬───────────────┘  │
│                              │                  │
│         ┌────────────────────┼──────────┐       │
│         ▼                    ▼          ▼       │
│  ┌──────────────┐  ┌──────────────┐ ┌────────┐ │
│  │MarkdownParser│  │LinkValidator │ │ GitHub │ │
│  │   Service    │  │  Service     │ │RepoSvc │ │
│  └──────────────┘  └──────────────┘ └────────┘ │
└─────────────────────────────────────────────────┘
```

### 核心元件

| 元件 | 說明 |
|------|------|
| **LinkCheckSseEndpoint** | SSE 端點，處理 POST 請求並串流檢測進度 |
| **LinkCheckOrchestrator** | 協調器，統籌整個連結檢測流程 |
| **MarkdownParserService** | 使用 [Markdig](https://github.com/xoofx/markdig) 解析 Markdown 連結 |
| **LinkValidatorService** | 驗證連結有效性（HTTP HEAD/GET、檔案存在性、錨點比對） |
| **GitHubRepoService** | 透過 GitHub API 存取公開 Repo 的檔案內容 |
| **AnchorSuggestionService** | 計算錨點編輯距離，提供拼字建議 |
| **ReportGeneratorService** | 產生檢測報告與 Markdown 格式匯出 |

### 主要依賴

| 套件 | 用途 |
|------|------|
| [Markdig](https://github.com/xoofx/markdig) | Markdown 解析引擎 |
| [Serilog](https://serilog.net/) | 結構化日誌記錄（Console + File） |

## 開發指南

### 專案結構

```
MarkDownLinkCheck/
├── Endpoints/          # API 端點（SSE）
├── Models/             # 資料模型（CheckRequest, Link, LinkResult...）
├── Services/           # 核心服務（解析、驗證、報告產生）
├── Pages/              # Razor Pages（UI）
├── wwwroot/            # 靜態資源（CSS, JS）
├── Program.cs          # 應用程式進入點與 DI 設定
└── appsettings.json    # 組態設定
MarkDownLinkCheck.Tests/
├── Unit/               # 單元測試
└── Integration/        # 整合測試
```

### 建構與測試

```bash
# 建構專案
dotnet build MarkDownLinkCheck/MarkDownLinkCheck.csproj

# 執行測試
dotnet test MarkDownLinkCheck.Tests/MarkDownLinkCheck.Tests.csproj
```

### 組態設定

可在 `appsettings.json` 的 `LinkCheck` 區段調整檢測行為：

| 設定項 | 預設值 | 說明 |
|--------|--------|------|
| `MaxMarkdownLength` | 100,000 | Markdown 內容字數上限 |
| `MaxFilesPerRepo` | 500 | 單一 Repo 最大掃描檔案數 |
| `MaxLinksPerFile` | 1,000 | 單一檔案最大連結解析數 |
| `MaxLinksPerCheck` | 5,000 | 單次檢測最大連結數 |
| `HttpTimeoutSeconds` | 10 | HTTP 請求逾時秒數 |
| `MaxRedirects` | 5 | 最大重導向次數 |
| `MaxRetries` | 1 | 逾時/5xx 最大重試次數 |
| `GlobalConcurrency` | 20 | 全域最大並行 HTTP 請求數 |
| `PerDomainConcurrency` | 3 | 每個網域最大並行請求數 |
| `UserAgent` | `MarkdownLinkCheck/1.0` | HTTP 請求 User-Agent 標頭 |

### 安全機制

- **SSRF 防護**：阻擋對私有 IP 位址的連線（`127.0.0.0/8`、`10.0.0.0/8`、`172.16.0.0/12`、`192.168.0.0/16`）
- **IP 速率限制**：同一 IP 每分鐘最多 5 次檢測請求
- **並行控制**：全域上限 20 個並行請求，單一網域上限 3 個
- **輸入驗證**：限制內容長度、檔案數量、連結數量上限
