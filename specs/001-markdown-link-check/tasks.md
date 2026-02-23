# Tasks: Markdown Link Check

**Input**: Design documents from `/specs/001-markdown-link-check/`  
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: 依據 Constitution 原則「II. 測試優先開發」及 plan.md 測試專案結構，包含單元測試與整合測試任務。

**Organization**: 任務依 User Story 分組，對應 spec.md 中的 P1～P4 優先順序。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 可平行執行（不同檔案、無相依性）
- **[Story]**: 所屬 User Story（US1、US2、US3、US4）
- 所有檔案路徑相對於 Repository 根目錄

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: 專案初始化與基本結構建立

- [X] T001 Create project directories (Models/, Services/, Endpoints/) in MarkDownLinkCheck/
- [X] T002 Add NuGet packages (Markdig, Serilog.AspNetCore, Serilog.Sinks.Console, Serilog.Sinks.File) to MarkDownLinkCheck/MarkDownLinkCheck.csproj
- [X] T003 [P] Create test project with xUnit, Moq, Microsoft.AspNetCore.Mvc.Testing in MarkDownLinkCheck.Tests/MarkDownLinkCheck.Tests.csproj and add to MarkDownLinkCheck.slnx
- [X] T004 [P] Configure LinkCheck options section and Serilog settings in MarkDownLinkCheck/appsettings.json and MarkDownLinkCheck/appsettings.Development.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: 所有 User Story 共用的核心模型、服務介面與 DI 設定

**⚠️ CRITICAL**: 所有 User Story 的工作必須等此階段完成後才能開始

- [X] T005 [P] Create LinkType enum (ExternalUrl, RelativePath, Anchor, Email, Image) in MarkDownLinkCheck/Models/LinkType.cs
- [X] T006 [P] Create LinkStatus enum (Healthy, Broken, Warning, Skipped) in MarkDownLinkCheck/Models/LinkStatus.cs
- [X] T007 [P] Create CheckMode enum (MarkdownSource, RepoUrl) in MarkDownLinkCheck/Models/CheckMode.cs
- [X] T008 [P] Create Link model (Type, RawText, TargetUrl, LineNumber, SourceFile) per data-model.md in MarkDownLinkCheck/Models/Link.cs
- [X] T009 [P] Create MarkdownFile model (FileName, RelativePath, Content, Links) per data-model.md in MarkDownLinkCheck/Models/MarkdownFile.cs
- [X] T010 [P] Create LinkResult model (Link, Status, HttpStatusCode, ErrorType, ErrorMessage, RedirectUrl, AnchorSuggestion, Duration) per data-model.md in MarkDownLinkCheck/Models/LinkResult.cs
- [X] T011 [P] Create CheckRequest model with conditional validation (Mode, MarkdownContent, RepoUrl, Branch, RequestedAt, SourceIp) per data-model.md in MarkDownLinkCheck/Models/CheckRequest.cs
- [X] T012 [P] Create CheckReport and FileCheckResult models (FileCount, TotalLinkCount, status counts, TotalDuration, FileResults) per data-model.md in MarkDownLinkCheck/Models/CheckReport.cs
- [X] T013 [P] Create CheckProgress SSE event model (EventType, CheckedCount, TotalCount, CurrentFile, FileResult, Report, ErrorMessage) per data-model.md in MarkDownLinkCheck/Models/CheckProgress.cs
- [X] T014 [P] Create LinkCheckOptions configuration model (MaxMarkdownLength, MaxFilesPerRepo, MaxLinksPerFile, MaxLinksPerCheck, HttpTimeoutSeconds, MaxRedirects, MaxRetries, GlobalConcurrency, PerDomainConcurrency, UserAgent) in MarkDownLinkCheck/Models/LinkCheckOptions.cs
- [X] T015 [P] Create service interfaces per data-model.md: IMarkdownParserService.cs, ILinkValidatorService.cs, IGitHubRepoService.cs, ILinkCheckOrchestrator.cs, IAnchorSuggestionService.cs, IReportGeneratorService.cs in MarkDownLinkCheck/Services/
- [X] T016 Configure Program.cs with Serilog bootstrap, LinkCheckOptions binding, IHttpClientFactory (Named "LinkChecker" with SocketsHttpHandler), Anti-Forgery, service DI, and SSE endpoint routing in MarkDownLinkCheck/Program.cs

**Checkpoint**: 基礎建設就緒 — User Story 實作可以開始

---

## Phase 3: User Story 1 — 以 Markdown 原始碼檢測連結 (Priority: P1) 🎯 MVP

**Goal**: 使用者貼上 Markdown 原始碼，系統解析所有連結並行驗證，透過 SSE 即時串流進度，輸出分類報告

**Independent Test**: 貼上一段包含有效連結、失效連結、錨點連結、Code Block 內連結的 Markdown 文字，驗證系統正確分類每個連結狀態

### Tests for User Story 1 ⚠️

> **NOTE**: 先撰寫測試並確認 FAIL，再實作功能

- [X] T017 [P] [US1] Write unit tests for MarkdownParserService (inline link, reference-style, auto-link, image link, code block exclusion, HTML comment exclusion, anchor extraction, 1000 link limit) in MarkDownLinkCheck.Tests/Unit/Services/MarkdownParserServiceTests.cs
- [X] T018 [P] [US1] Write unit tests for AnchorSuggestionService (exact match, Levenshtein distance ≤ 2, no suggestion when distance > 2) in MarkDownLinkCheck.Tests/Unit/Services/AnchorSuggestionServiceTests.cs
- [X] T019 [P] [US1] Write unit tests for LinkValidatorService (HTTP 2xx=Healthy, 4xx/5xx=Broken, 301=Warning with RedirectUrl, 302=Healthy, 429=Warning, timeout=Warning, HEAD 405 fallback to GET, anchor validation, email format, skip relative path in Markdown mode, SSRF blocked) in MarkDownLinkCheck.Tests/Unit/Services/LinkValidatorServiceTests.cs
- [X] T020 [P] [US1] Write unit tests for ReportGeneratorService (status counts, file grouping, Broken-first sorting) in MarkDownLinkCheck.Tests/Unit/Services/ReportGeneratorServiceTests.cs
- [X] T021 [P] [US1] Write unit tests for LinkCheckOrchestrator (MarkdownSource mode, progress events, URL deduplication, 5000 link limit, 100000 char limit) in MarkDownLinkCheck.Tests/Unit/Services/LinkCheckOrchestratorTests.cs
- [X] T022 [P] [US1] Write unit tests for Link model (type determination logic: mailto→Email, #→Anchor, http→ExternalUrl, else→RelativePath, image detection) in MarkDownLinkCheck.Tests/Unit/Models/LinkTests.cs

### Implementation for User Story 1

- [X] T023 [P] [US1] Implement MarkdownParserService using Markdig AST traversal (ParseLinks: LinkInline + AutolinkInline extraction, ExtractAnchors: HeadingBlock→GitHub-style anchor, reference-style link resolution) in MarkDownLinkCheck/Services/MarkdownParserService.cs
- [X] T024 [P] [US1] Implement AnchorSuggestionService (Levenshtein distance calculation, suggest when distance ≤ 2, return closest match) in MarkDownLinkCheck/Services/AnchorSuggestionService.cs
- [X] T025 [US1] Implement LinkValidatorService (IHttpClientFactory "LinkChecker", HEAD request with GET fallback on 405, status mapping per data-model.md, anchor validation with AnchorSuggestionService, email format check, skip relative path in Markdown mode, retry once on timeout/5xx) in MarkDownLinkCheck/Services/LinkValidatorService.cs
- [X] T026 [US1] Implement ReportGeneratorService (GenerateReport: aggregate FileCheckResult with status counts and duration, sort LinkResults Broken→Warning→Healthy, sort FileResults by severity) in MarkDownLinkCheck/Services/ReportGeneratorService.cs
- [X] T027 [US1] Implement LinkCheckOrchestrator MarkdownSource mode (create single MarkdownFile, parse links, deduplicate external URLs, validate all links, yield CheckProgress events via IAsyncEnumerable, yield file-result then complete) in MarkDownLinkCheck/Services/LinkCheckOrchestrator.cs
- [X] T028 [US1] Implement SSE streaming endpoint POST /api/check/sse (set text/event-stream headers, deserialize CheckRequest, call orchestrator, write SSE event format, handle cancellation, return 400 on validation error) per contracts/sse-endpoint.md in MarkDownLinkCheck/Endpoints/LinkCheckSseEndpoint.cs
- [X] T029 [US1] Update Index.cshtml with mode selection radio buttons (MarkdownSource/RepoUrl), Markdown textarea with 100,000 char limit, start button, progress bar area, file results container, report summary area, error message display in MarkDownLinkCheck/Pages/Index.cshtml
- [X] T030 [US1] Update Index.cshtml.cs PageModel (remove default handler boilerplate, page serves as static entry point) in MarkDownLinkCheck/Pages/Index.cshtml.cs
- [X] T031 [US1] Implement SSE client using Fetch API + ReadableStream (POST to /api/check/sse, parse SSE event format, handle progress/file-result/complete/error events, update progress bar, append file results, show errors, AbortController for cancel) in MarkDownLinkCheck/wwwroot/js/site.js
- [X] T032 [US1] Add report display styles (status icons ✅❌⚠️⏭️, file grouping cards, link result rows, progress bar, error message styling) in MarkDownLinkCheck/wwwroot/css/site.css
- [X] T033 [US1] Update shared layout with application title "Markdown Link Check", remove Privacy nav link, update footer in MarkDownLinkCheck/Pages/Shared/_Layout.cshtml

**Checkpoint**: User Story 1 完整可用 — 使用者可貼上 Markdown 文字即時檢測所有連結

---

## Phase 4: User Story 2 — 以 GitHub Repo URL 檢測連結 (Priority: P2)

**Goal**: 使用者輸入公開 GitHub Repo URL，系統自動抓取所有 .md 檔案、解析連結、驗證相對路徑與跨檔案錨點

**Independent Test**: 輸入已知的公開 GitHub Repo URL，驗證系統掃描 .md 檔案、解析連結、檢查相對路徑及跨檔案錨點正確性

### Tests for User Story 2 ⚠️

- [X] T034 [P] [US2] Write unit tests for GitHubRepoService (ValidateRepoUrlAsync: valid/invalid URL/private repo, GetDefaultBranchAsync, ListMarkdownFilesAsync: 500 file limit, GetFileContentAsync) with mocked IHttpClientFactory in MarkDownLinkCheck.Tests/Unit/Services/GitHubRepoServiceTests.cs

### Implementation for User Story 2

- [X] T035 [US2] Implement GitHubRepoService (GitHub REST API v3, validate repo URL regex, get default branch, recursively list .md files via git tree API, get file content via raw content API, handle 404 for private repos, handle rate limit 403) in MarkDownLinkCheck/Services/GitHubRepoService.cs
- [X] T036 [US2] Extend LinkCheckOrchestrator with RepoUrl mode (call GitHubRepoService to list and fetch .md files, build repoFiles set and anchorsMap for relative path/anchor validation, yield progress per file, enforce 500 file limit and 5000 link limit) in MarkDownLinkCheck/Services/LinkCheckOrchestrator.cs
- [X] T037 [US2] Extend LinkValidatorService for relative path validation in repo context (check file existence in repoFiles set, cross-file anchor validation using anchorsMap, anchor suggestion for cross-file anchors) in MarkDownLinkCheck/Services/LinkValidatorService.cs
- [X] T038 [US2] Add RepoUrl mode UI (URL input field with pattern validation, optional branch name input, mode toggle show/hide, validation error messages) to MarkDownLinkCheck/Pages/Index.cshtml
- [X] T039 [US2] Add RepoUrl mode handling in SSE client (send repoUrl and branch in payload, display "scanning... found N files", multi-file result display with file path headers) in MarkDownLinkCheck/wwwroot/js/site.js
- [X] T040 [US2] Update orchestrator unit tests for RepoUrl mode (multi-file scanning, relative path context, cross-file anchor, file limit, rate limit error) in MarkDownLinkCheck.Tests/Unit/Services/LinkCheckOrchestratorTests.cs

**Checkpoint**: User Story 1 與 2 皆可獨立運作 — 支援 Markdown 原始碼與 GitHub Repo URL 兩種模式

---

## Phase 5: User Story 3 — 檢查報告與匯出 (Priority: P3)

**Goal**: 使用者查看按檔案分組、錯誤優先排序的完整報告，並可一鍵複製為 Markdown 格式

**Independent Test**: 觸發一次完整檢測後，驗證報告結構（檔案分組、狀態排序、行號標示、統計摘要）及「複製為 Markdown」功能的正確性

### Tests for User Story 3 ⚠️

> **NOTE**: 先撰寫測試並確認 FAIL，再實作功能

- [X] T041a [P] [US3] Write unit tests for ReportGeneratorService.GenerateMarkdownReport (Markdown table output format, status emoji mapping ✅❌⚠️⏭️, file headers, target URL, line number, error message with anchor suggestion "did you mean #xxx?", summary statistics, total duration formatting) in MarkDownLinkCheck.Tests/Unit/Services/ReportGeneratorServiceTests.cs

### Implementation for User Story 3

- [X] T041 [US3] Implement GenerateMarkdownReport in ReportGeneratorService (output Markdown table format: file headers, status emoji, target URL, line number, error message, anchor suggestion, summary statistics, total duration) in MarkDownLinkCheck/Services/ReportGeneratorService.cs
- [X] T042 [US3] Enhance report rendering with detailed link info (HTTP status code, error type, redirect URL, anchor suggestion "did you mean #xxx?", line number display) in MarkDownLinkCheck/wwwroot/js/site.js
- [X] T043 [US3] Implement "Copy as Markdown" button (call GenerateMarkdownReport via hidden field or client-side generation, Clipboard API writeText, success/failure toast notification) in MarkDownLinkCheck/wwwroot/js/site.js
- [X] T044 [US3] Add report summary section (scanned file count, total link count, Healthy/Broken/Warning/Skipped counts, total duration, "Copy as Markdown" button) to MarkDownLinkCheck/Pages/Index.cshtml
- [X] T045 [US3] Style report summary statistics (count badges with status colors), copy button, and toast notification in MarkDownLinkCheck/wwwroot/css/site.css

**Checkpoint**: 完整報告功能就緒 — 使用者可查看詳細報告並複製為 Markdown 貼到 GitHub Issue/PR

---

## Phase 6: User Story 4 — 速率控制與安全防護 (Priority: P4)

**Goal**: 系統自動遵循速率控制規則，防範 SSRF 等安全風險，確保穩定運行

**Independent Test**: 模擬大量連結檢測驗證並行數量限制與同網域限制生效，確認私有 IP 位址被拒絕，驗證同一 IP 超過 5 次/分鐘被限制

### Tests for User Story 4 ⚠️

> **NOTE**: 先撰寫測試並確認 FAIL，再實作功能

- [X] T045a [P] [US4] Write unit tests for SSRF protection (ConnectCallback blocks private IPs: 127.0.0.0/8, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, ::1; allows public IPs), per-domain SemaphoreSlim concurrency (max 3 per hostname per FR-031), and IP-based rate limiting middleware (max 5/min per IP, returns HTTP 429 with retryAfter) in MarkDownLinkCheck.Tests/Unit/Services/SecurityAndRateLimitTests.cs
- [X] T045b [P] [US4] Write integration test for SSRF ConnectCallback using WebApplicationFactory (POST /api/check/sse with Markdown containing loopback/private-IP URLs → verify LinkResult status=Broken, errorType=ssrf_blocked) in MarkDownLinkCheck.Tests/Integration/Endpoints/LinkCheckSseEndpointTests.cs

### Implementation for User Story 4

- [X] T046 [P] [US4] Implement SSRF protection via SocketsHttpHandler ConnectCallback (resolve DNS, check if IP is private: 127.0.0.0/8, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, ::1, block and throw) in MarkDownLinkCheck/Program.cs
- [X] T047 [P] [US4] Implement per-domain concurrency control (ConcurrentDictionary<string, SemaphoreSlim> keyed by hostname, max 3 per domain per FR-031) in MarkDownLinkCheck/Services/LinkValidatorService.cs
- [X] T048 [US4] Implement global concurrency control (SemaphoreSlim max 20 per FR-032, wrap link validation calls) in MarkDownLinkCheck/Services/LinkCheckOrchestrator.cs
- [X] T049 [US4] Implement IP-based rate limiting middleware (track request count per source IP, max 5 per minute per FR-036, return HTTP 429 with JSON error message and retryAfter) in MarkDownLinkCheck/Program.cs
- [X] T050 [P] [US4] Add rate limit exceeded (HTTP 429) and SSRF error response handling with user-friendly messages in MarkDownLinkCheck/wwwroot/js/site.js

**Checkpoint**: 安全防護與速率控制完成 — 系統可安全穩定地對外運行

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: 跨 User Story 的品質改善與最終驗證

- [X] T051 [P] Write integration tests for SSE endpoint (full pipeline: POST request → SSE stream → progress/file-result/complete events, validation error 400, rate limit 429) using WebApplicationFactory in MarkDownLinkCheck.Tests/Integration/Endpoints/LinkCheckSseEndpointTests.cs
- [X] T052 [P] Write integration tests for Index page (page loads, form elements present, client validation) using WebApplicationFactory in MarkDownLinkCheck.Tests/Integration/Pages/IndexPageTests.cs
- [X] T053 [P] Update Error page with styled error display and user-friendly message in MarkDownLinkCheck/Pages/Error.cshtml
- [X] T054 Code cleanup, add XML doc comments to all public APIs, verify file-scoped namespaces, apply .editorconfig formatting
- [X] T055 Run quickstart.md validation: dotnet build, dotnet run, dotnet test, verify all tests pass
- [X] T055a [P] Write performance benchmark tests for SC-001 (50 links Markdown mode < 30s) and SC-002 (10 files / 200 links Repo mode < 2min) using WebApplicationFactory with mocked HTTP responses and Stopwatch assertions in MarkDownLinkCheck.Tests/Integration/Endpoints/LinkCheckSsePerformanceTests.cs

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 無相依性 — 可立即開始
- **Foundational (Phase 2)**: 依賴 Setup 完成 — **阻擋所有 User Story**
- **User Stories (Phase 3–6)**: 全部依賴 Foundational 完成
  - US1 (P1) → US2 (P2) → US3 (P3) → US4 (P4) 依優先順序執行
  - US2 部分依賴 US1 的 MarkdownParserService 與 LinkValidatorService
  - US3 依賴 US1 的基本報告渲染框架
  - US4 可在 US1 完成後獨立實作（增強既有服務）
- **Polish (Phase 7)**: 依賴所有 User Story 完成

### User Story Dependencies

- **User Story 1 (P1)**: Foundational 完成後可開始 — 不依賴其他 Story
- **User Story 2 (P2)**: 依賴 US1 的 MarkdownParserService、LinkValidatorService、LinkCheckOrchestrator 基礎實作
- **User Story 3 (P3)**: 依賴 US1 的基本報告框架，增強報告呈現與匯出
- **User Story 4 (P4)**: 依賴 US1 的 LinkValidatorService 與 LinkCheckOrchestrator，加入安全與速率控制

### Within Each User Story

- 測試 MUST 先撰寫並確認 FAIL，再實作功能
- Models → Services → Endpoints → UI/JS
- 核心實作完成後再整合
- 每個 Story 完成後驗證可獨立測試

### Parallel Opportunities

- **Phase 1**: T003, T004 可平行
- **Phase 2**: T005–T015 全部可平行（不同檔案）
- **Phase 3 Tests**: T017–T022 全部可平行
- **Phase 3 Impl**: T023, T024 可平行
- **Phase 4 Tests**: T034 獨立
- **Phase 5 Tests**: T041a 獨立
- **Phase 6 Tests**: T045a, T045b 可平行
- **Phase 6 Impl**: T046, T047 可平行；T050 獨立
- **Phase 7**: T051, T052, T053, T055a 可平行

---

## Parallel Example: User Story 1

```bash
# 1. Launch all tests in parallel (should FAIL):
Task T017: "Unit tests for MarkdownParserService"
Task T018: "Unit tests for AnchorSuggestionService"
Task T019: "Unit tests for LinkValidatorService"
Task T020: "Unit tests for ReportGeneratorService"
Task T021: "Unit tests for LinkCheckOrchestrator"
Task T022: "Unit tests for Link model"

# 2. Launch independent services in parallel:
Task T023: "Implement MarkdownParserService"
Task T024: "Implement AnchorSuggestionService"

# 3. Sequential implementation (dependencies):
Task T025: "Implement LinkValidatorService" (depends on T024 AnchorSuggestionService)
Task T026: "Implement ReportGeneratorService"
Task T027: "Implement LinkCheckOrchestrator" (depends on T023, T025, T026)
Task T028: "Implement SSE endpoint" (depends on T027)

# 4. Launch UI tasks in parallel:
Task T029: "Update Index.cshtml"
Task T030: "Update Index.cshtml.cs"
Task T032: "Add report styles in site.css"
Task T033: "Update shared layout"

# 5. SSE client (depends on endpoint being defined):
Task T031: "Implement SSE client in site.js"
```

## Parallel Example: User Story 2

```bash
# 1. Tests first:
Task T034: "Unit tests for GitHubRepoService"

# 2. Sequential implementation:
Task T035: "Implement GitHubRepoService"
Task T036: "Extend LinkCheckOrchestrator for RepoUrl mode" (depends on T035)
Task T037: "Extend LinkValidatorService for relative paths" (depends on T036)

# 3. UI updates in parallel:
Task T038: "Add RepoUrl mode UI"
Task T039: "Add RepoUrl mode in site.js"

# 4. Update tests:
Task T040: "Update orchestrator tests for RepoUrl"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: 貼上 Markdown 文字驗證完整流程
5. Deploy/demo if ready — 使用者已可獲得即時價值

### Incremental Delivery

1. Setup + Foundational → 基礎建設就緒
2. User Story 1 → 獨立測試 → Deploy/Demo（**MVP!**）
3. User Story 2 → 獨立測試 → Deploy/Demo（支援 GitHub Repo 掃描）
4. User Story 3 → 獨立測試 → Deploy/Demo（完整報告與匯出）
5. User Story 4 → 獨立測試 → Deploy/Demo（安全與速率控制）
6. Polish → 整合測試、程式碼品質、最終驗證

### Parallel Team Strategy

With multiple developers:

1. 團隊共同完成 Setup + Foundational
2. Foundational 完成後：
   - Developer A: User Story 1（核心引擎）
   - Developer B: User Story 4（安全防護，可先寫測試）
3. US1 完成後：
   - Developer A: User Story 2（GitHub 整合）
   - Developer B: User Story 3（報告匯出）
4. 最後共同完成 Polish

---

## Notes

- [P] 標記 = 不同檔案、無相依性，可平行執行
- [Story] 標記 = 對應 spec.md 中的 User Story，確保可追溯性
- 每個 User Story 應可獨立完成與測試
- 每個 Task 完成後 commit
- 在每個 Checkpoint 停下驗證功能正確性
- 避免：模糊任務描述、同一檔案衝突、破壞獨立性的跨 Story 相依
