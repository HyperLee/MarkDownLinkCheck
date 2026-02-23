# 快速入門指南：Markdown Link Check

**Feature Branch**: `001-markdown-link-check`  
**建立日期**: 2026-02-23  
**狀態**: 完成

---

## 前置需求

| 項目 | 版本 | 用途 |
|------|------|------|
| .NET SDK | 10.0+ | 編譯與執行 |
| Node.js | 不需要 | 前端使用 CDN / wwwroot/lib 靜態檔案 |
| 資料庫 | 不需要 | 所有資料為記憶體內靜態集合 |

---

## 環境設定

### 1. 複製專案

```bash
git clone https://github.com/HyperLee/MarkDownLinkCheck.git
cd MarkDownLinkCheck
git checkout 001-markdown-link-check
```

### 2. 還原相依套件

```bash
dotnet restore
```

### 3. 驗證建構

```bash
dotnet build
```

---

## 專案結構概覽

```
MarkDownLinkCheck/                 # 主要 Razor Pages 專案
├── Program.cs                     # 啟動與 DI 註冊
├── Models/                        # 資料模型（CheckRequest、Link、LinkResult 等）
├── Services/                      # 業務邏輯（Markdown 解析、連結驗證、GitHub 掃描）
├── Endpoints/                     # Minimal API SSE 端點
├── Pages/                         # Razor Pages 頁面
│   ├── Index.cshtml               # 首頁（檢測入口）
│   └── Shared/_Layout.cshtml      # 共用版面配置
├── wwwroot/                       # 靜態資源
│   ├── css/site.css               # 自訂樣式
│   └── js/site.js                 # SSE 客戶端、報告渲染
└── appsettings.json               # 設定檔（含 Serilog、LinkCheck 選項）

MarkDownLinkCheck.Tests/           # 測試專案
├── Unit/                          # 單元測試
└── Integration/                   # 整合測試
```

---

## 執行應用程式

### 開發模式

```bash
cd MarkDownLinkCheck
dotnet run
```

預設 URL：
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

開啟瀏覽器存取首頁即可開始使用。

### 環境設定檔

| 檔案 | 用途 |
|------|------|
| `appsettings.json` | 通用設定（Serilog、LinkCheck 選項） |
| `appsettings.Development.json` | 開發環境設定（Debug 日誌等級） |

---

## 關鍵設定

### LinkCheck 選項（appsettings.json）

```json
{
  "LinkCheck": {
    "MaxMarkdownLength": 100000,
    "MaxFilesPerRepo": 500,
    "MaxLinksPerFile": 1000,
    "MaxLinksPerCheck": 5000,
    "HttpTimeoutSeconds": 10,
    "MaxRedirects": 5,
    "MaxRetries": 1,
    "GlobalConcurrency": 20,
    "PerDomainConcurrency": 3,
    "UserAgent": "MarkdownLinkCheck/1.0"
  }
}
```

### Serilog 設定

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/log-.txt", "rollingInterval": "Day" } }
    ]
  }
}
```

---

## 執行測試

### 全部測試

```bash
dotnet test
```

### 僅單元測試

```bash
dotnet test --filter "Category=Unit"
```

### 僅整合測試

```bash
dotnet test --filter "Category=Integration"
```

---

## 核心開發流程

### 新增服務

1. 在 `Services/` 中定義介面（`I{ServiceName}.cs`）
2. 在 `Services/` 中實作服務（`{ServiceName}.cs`）
3. 在 `Program.cs` 中註冊 DI
4. 撰寫對應的單元測試至 `Tests/Unit/Services/`

### 服務註冊範例

```csharp
// Program.cs
builder.Services.AddScoped<IMarkdownParserService, MarkdownParserService>();
builder.Services.AddScoped<ILinkValidatorService, LinkValidatorService>();
builder.Services.AddScoped<IGitHubRepoService, GitHubRepoService>();
builder.Services.AddScoped<ILinkCheckOrchestrator, LinkCheckOrchestrator>();
builder.Services.AddScoped<IAnchorSuggestionService, AnchorSuggestionService>();
builder.Services.AddScoped<IReportGeneratorService, ReportGeneratorService>();
```

### TDD 流程

1. 先撰寫預期失敗的測試
2. 實作最小功能使測試通過
3. 重構程式碼保持品質
4. 確認所有現有測試仍通過

---

## 主要 NuGet 套件

| 套件 | 用途 |
|------|------|
| `Markdig` | Markdown 解析與 AST 遍歷 |
| `Serilog.AspNetCore` | 結構化日誌整合 |
| `Serilog.Sinks.Console` | Console 日誌輸出 |
| `Serilog.Sinks.File` | File 日誌輸出（rolling） |

### 測試專案套件

| 套件 | 用途 |
|------|------|
| `xunit` | 單元測試框架 |
| `Moq` | Mocking 框架 |
| `Microsoft.AspNetCore.Mvc.Testing` | WebApplicationFactory 整合測試 |

---

## 注意事項

- **無資料庫**：所有資料為檢測過程中的記憶體內物件，不持久化
- **無認證**：使用者無需登入即可使用（FR 假設）
- **GitHub API Rate Limit**：未認證情況下每小時 60 次 API 呼叫，每次 Repo 掃描消耗約 2 次
- **SSRF 防護**：`SocketsHttpHandler.ConnectCallback` 會阻擋對私有 IP 位址的連線
- **SSE 串流**：使用 Fetch API（非 EventSource），支援 POST 請求傳送大量內容
