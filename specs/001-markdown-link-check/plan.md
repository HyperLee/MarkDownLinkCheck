# Implementation Plan: Markdown Link Check

**Branch**: `001-markdown-link-check` | **Date**: 2026-02-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-markdown-link-check/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

建構一個 ASP.NET Core Razor Pages Web 應用程式，提供一鍵檢測 Markdown 文件中失效連結的功能。支援兩種模式：(1) 直接貼上 Markdown 原始碼即時檢測、(2) 輸入 GitHub Repo URL 自動掃描。系統透過 Server-Sent Events (SSE) 即時串流檢測進度，產生按檔案分組、依狀態排序的結構化報告，並提供「複製為 Markdown」匯出功能。技術方案採用 C# 14 + ASP.NET Core 10.0 + Serilog，以 `IHttpClientFactory` 管理 HTTP 連結驗證，內建 SSRF 防護與速率控制機制。

## Technical Context

**Language/Version**: C# 14 / .NET 10.0  
**Primary Dependencies**: ASP.NET Core 10.0、Bootstrap 5、jQuery 3.x、jQuery Validation、Serilog  
**Storage**: N/A（C# 靜態資料集合，無資料庫）  
**Testing**: xUnit + Moq（單元測試）+ WebApplicationFactory（整合測試）  
**Target Platform**: 桌面瀏覽器（Chrome、Edge、Firefox、Safari）  
**Project Type**: Web（單一 Razor Pages 專案）  
**Performance Goals**: FCP < 1.5 秒、LCP < 2.5 秒；50 連結 Markdown 檢測 < 30 秒；200 連結 Repo 檢測 < 2 分鐘  
**Constraints**: 僅桌面瀏覽器、不需行動裝置適配、不使用任何資料庫軟體與架構  
**Scale/Scope**: 單一使用者並行操作、單 Repo 最多 500 個 .md 檔案、單次最多 5,000 個連結

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| 原則 | 狀態 | 說明 |
|------|------|------|
| I. 程式碼品質至上 | ✅ PASS | C# 14 最新功能、檔案範圍命名空間、模式匹配、null 安全性；所有公開 API 附 XML 文件註解；遵循 `.editorconfig` 格式規範 |
| II. 測試優先開發 | ✅ PASS | 採用 xUnit + Moq 單元測試 + WebApplicationFactory 整合測試；關鍵路徑（Markdown 解析、連結驗證、報告產生）100% 測試覆蓋；Mock 隔離 HTTP 呼叫 |
| III. 使用者體驗一致性 | ✅ PASS | Bootstrap 5 元件統一設計語言；SSE 即時進度回饋；清晰錯誤訊息含 HTTP 狀態碼說明；jQuery Validation 即時驗證 |
| IV. 效能與延展性 | ✅ PASS | FCP < 1.5s / LCP < 2.5s；並行 HTTP 驗證（最多 20 並行）；`IHttpClientFactory` 管理 HttpClient；async/await 全程非同步 |
| V. 可觀察性與監控 | ✅ PASS | Serilog 結構化日誌；正確日誌層級使用；連結檢測事件記錄 |
| VI. 安全優先 | ✅ PASS | SSRF 防護（阻擋私有 IP）；輸入驗證（字數上限、URL 格式）；Razor 引擎 HTML 編碼；Anti-Forgery Token；HTTPS + HSTS |

**Constitution Gate 結果: ✅ 全部通過，無違規項目**

## Project Structure

### Documentation (this feature)

```text
specs/001-markdown-link-check/
├── plan.md              # 本文件（/speckit.plan 命令輸出）
├── research.md          # Phase 0 輸出（/speckit.plan 命令）
├── data-model.md        # Phase 1 輸出（/speckit.plan 命令）
├── quickstart.md        # Phase 1 輸出（/speckit.plan 命令）
├── contracts/           # Phase 1 輸出（/speckit.plan 命令）
└── tasks.md             # Phase 2 輸出（/speckit.tasks 命令 — 不由 /speckit.plan 建立）
```

### Source Code (repository root)

```text
MarkDownLinkCheck/
├── Program.cs                          # 應用程式啟動與 DI 註冊
├── appsettings.json                    # 應用程式設定
├── appsettings.Development.json        # 開發環境設定
├── Models/
│   ├── CheckRequest.cs                 # 檢測請求模型
│   ├── MarkdownFile.cs                 # Markdown 檔案模型
│   ├── Link.cs                         # 連結模型
│   ├── LinkResult.cs                   # 連結檢測結果模型
│   ├── CheckReport.cs                  # 檢測報告模型
│   ├── LinkType.cs                     # 連結類型列舉
│   └── LinkStatus.cs                   # 連結狀態列舉
├── Services/
│   ├── IMarkdownParserService.cs       # Markdown 解析介面
│   ├── MarkdownParserService.cs        # Markdown 解析實作
│   ├── ILinkValidatorService.cs        # 連結驗證介面
│   ├── LinkValidatorService.cs         # 連結驗證實作（含 SSRF 防護）
│   ├── IGitHubRepoService.cs           # GitHub Repo 掃描介面
│   ├── GitHubRepoService.cs            # GitHub Repo 掃描實作
│   ├── ILinkCheckOrchestrator.cs       # 檢測協調器介面
│   ├── LinkCheckOrchestrator.cs        # 檢測協調器實作（整合解析+驗證+報告）
│   ├── IAnchorSuggestionService.cs     # 錨點拼字建議介面
│   ├── AnchorSuggestionService.cs      # 錨點拼字建議實作
│   ├── IReportGeneratorService.cs      # 報告產生介面
│   └── ReportGeneratorService.cs       # 報告產生實作
├── Pages/
│   ├── Index.cshtml                    # 首頁（檢測入口）
│   ├── Index.cshtml.cs                 # 首頁 PageModel
│   ├── Error.cshtml                    # 錯誤頁面
│   ├── Error.cshtml.cs                 # 錯誤頁面 PageModel
│   ├── Privacy.cshtml                  # 隱私頁面
│   ├── Privacy.cshtml.cs               # 隱私頁面 PageModel
│   └── Shared/
│       ├── _Layout.cshtml              # 共用版面配置
│       ├── _Layout.cshtml.css          # 版面配置樣式
│       └── _ValidationScriptsPartial.cshtml
├── Endpoints/
│   └── LinkCheckSseEndpoint.cs         # SSE 串流端點（Minimal API）
├── wwwroot/
│   ├── css/site.css                    # 自訂全域樣式
│   ├── js/site.js                      # 自訂全域腳本（SSE 客戶端、報告渲染）
│   └── lib/                            # 第三方函式庫（Bootstrap、jQuery）
└── Properties/
    └── launchSettings.json

MarkDownLinkCheck.Tests/
├── Unit/
│   ├── Services/
│   │   ├── MarkdownParserServiceTests.cs
│   │   ├── LinkValidatorServiceTests.cs
│   │   ├── GitHubRepoServiceTests.cs
│   │   ├── AnchorSuggestionServiceTests.cs
│   │   ├── ReportGeneratorServiceTests.cs
│   │   └── LinkCheckOrchestratorTests.cs
│   └── Models/
│       └── LinkTests.cs
└── Integration/
    ├── Pages/
    │   └── IndexPageTests.cs
    └── Endpoints/
        └── LinkCheckSseEndpointTests.cs
```

**Structure Decision**: 採用單一 Razor Pages 專案結構，符合現有 `MarkDownLinkCheck.csproj` 專案佈局。Models/Services/Endpoints 以功能分層，測試專案獨立為 `MarkDownLinkCheck.Tests`，含 Unit 與 Integration 子目錄。SSE 端點以 Minimal API 形式掛載於 `Endpoints/`，與 Razor Pages 共存。

## Complexity Tracking

> 本功能無憲章違規項目，無需記錄複雜度追蹤。
