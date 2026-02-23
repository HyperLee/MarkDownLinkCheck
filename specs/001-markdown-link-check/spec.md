# Feature Specification: Markdown Link Check

**Feature Branch**: `001-markdown-link-check`  
**Created**: 2026-02-23  
**Status**: Draft  
**Input**: User description: "一鍵檢測 Markdown 文件中的失效連結，降低文件維護成本的 Web 應用程式"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 以 Markdown 原始碼檢測連結 (Priority: P1)

使用者開啟網站，選擇「Markdown 原始碼」模式，將一段 Markdown 內容貼入文字輸入區，點選「開始檢測」。系統解析貼上內容中的所有連結（外部 URL、錨點連結、郵件連結、圖片連結），並行驗證連結有效性，即時顯示檢測進度，最終輸出分類後的檢查報告。

**Why this priority**: 這是最基本的核心功能。不需要任何外部 API 整合即可獨立運作，使用者只需貼上文字就能獲得即時價值。為後續 Repo URL 模式奠定基礎（連結解析與驗證引擎是共用的）。

**Independent Test**: 可透過貼上一段包含有效與失效連結的 Markdown 文字來完整測試，驗證系統能正確分類並回報每一個連結的狀態。

**Acceptance Scenarios**:

1. **Given** 使用者在文字輸入區貼上一段包含 5 個外部 URL 的 Markdown 文字, **When** 點選「開始檢測」, **Then** 系統逐一驗證每個 URL 並即時顯示進度「已檢查 N / 共 5 個連結」，最終報告中正確標示每個連結的狀態（✅ Healthy / ❌ Broken / ⚠️ Warning）
2. **Given** 使用者貼上的 Markdown 中包含 Code Block 內的 URL, **When** 執行檢測, **Then** Code Block 內的 URL 被標記為 ⏭️ Skipped，不發送 HTTP 請求
3. **Given** 使用者貼上的 Markdown 中包含相對路徑連結（如 `./docs/setup.md`）, **When** 執行檢測, **Then** 該連結被標記為 ⏭️ Skipped 並附上原因「缺少 Repo 上下文，無法驗證」
4. **Given** 使用者貼上的 Markdown 中包含錨點連結 `#installation`, **When** 該錨點對應到文件中某個實際標題, **Then** 該連結被標記為 ✅ Healthy
5. **Given** 使用者貼上的 Markdown 中包含錨點連結 `#installatoin`（拼錯）, **When** 該錨點不對應到任何標題但有相似候選項, **Then** 報告中顯示「❌ anchor not found — did you mean #installation?」
6. **Given** 使用者貼上超過 100,000 字元的 Markdown, **When** 點選「開始檢測」, **Then** 系統顯示友善錯誤訊息提示已超過字數上限

---

### User Story 2 - 以 GitHub Repo URL 檢測連結 (Priority: P2)

使用者開啟網站，選擇「Repo URL」模式，貼上一個公開的 GitHub Repository URL（如 `https://github.com/owner/repo`），可選擇指定分支，點選「開始檢測」。系統自動抓取該 Repo 中所有 `.md` 檔案，逐一解析檔案中的連結，並行驗證連結有效性，即時顯示進度，輸出依檔案分組的檢查報告。

**Why this priority**: 這是產品的核心差異化功能，直接對應目標使用者（開源專案維護者）的主要需求。依賴 P1 的連結解析引擎，並額外需要 GitHub API 整合。

**Independent Test**: 可透過輸入一個已知的公開 GitHub Repo URL 來測試，驗證系統能抓取 .md 檔案清單、解析連結、檢查相對路徑及跨檔案錨點。

**Acceptance Scenarios**:

1. **Given** 使用者輸入一個合法的公開 GitHub Repo URL, **When** 點選「開始檢測」, **Then** 系統顯示「正在掃描...找到 N 個 Markdown 檔案」，逐一檢查每個檔案的連結並即時串流結果
2. **Given** 使用者輸入的 Repo 中有 `.md` 檔案包含相對路徑連結 `./docs/setup.md`, **When** 該檔案存在於 Repo 中, **Then** 連結被標記為 ✅ Healthy
3. **Given** 使用者輸入的 Repo 中有 `.md` 檔案包含相對路徑連結 `./docs/missing.md`, **When** 該檔案不存在於 Repo 中, **Then** 連結被標記為 ❌ Broken 並附上原因「file not found」
4. **Given** 使用者輸入的 Repo 中有跨檔案錨點連結 `./docs/setup.md#prerequisites`, **When** 檔案存在但錨點不存在, **Then** 連結被標記為 ❌ Broken 並附上原因「anchor not found」
5. **Given** 使用者輸入一個包含超過 500 個 `.md` 檔案的 Repo, **When** 開始檢測, **Then** 系統提示「檔案數量超過上限（500），僅掃描前 500 個檔案」
6. **Given** 使用者輸入一個不合法的 URL 或非 GitHub URL, **When** 點選「開始檢測」, **Then** 系統顯示友善錯誤訊息「請輸入合法的 GitHub Repository URL」
7. **Given** 使用者輸入一個私有 Repo 的 URL, **When** 點選「開始檢測」, **Then** 系統顯示友善錯誤訊息「目前僅支援公開 Repository」
8. **Given** 使用者選擇指定分支名稱, **When** 開始檢測, **Then** 系統以該分支的內容進行掃描

---

### User Story 3 - 檢查報告與匯出 (Priority: P3)

使用者在檢測完成後，查看按檔案分組、錯誤優先排序的完整報告，包含連結狀態、行號、錯誤類型等資訊。使用者可使用「複製為 Markdown」按鈕將報告複製到剪貼簿，方便貼到 Issue 或 PR Comment 中。

**Why this priority**: 報告呈現品質直接影響使用者體驗與產品實用性，但功能上依賴 P1/P2 的檢測引擎先行完成。

**Independent Test**: 可透過觸發一次完整檢測後，驗證報告結構（檔案分組、狀態排序、行號標示）以及複製功能的正確性。

**Acceptance Scenarios**:

1. **Given** 一次檢測已完成, **When** 使用者查看報告, **Then** 結果依檔案分組，每個檔案中 ❌ Broken 排最前、⚠️ Warning 次之、✅ Healthy 數量摘要放最後
2. **Given** 報告中有失效連結, **When** 查看該連結的詳細資訊, **Then** 顯示狀態碼或錯誤類型、連結網址、所在行號
3. **Given** 檢測完成, **When** 使用者點選「複製為 Markdown」按鈕, **Then** 報告以 Markdown 格式複製到剪貼簿
4. **Given** 檢測完成, **When** 使用者查看報告底部, **Then** 顯示總結統計（掃描檔案數、檢查連結數、各狀態數量）及總耗時

---

### User Story 4 - 速率控制與安全防護 (Priority: P4)

系統在檢測過程中自動遵循速率控制規則，避免對目標站點造成過大負載或觸發封鎖，並防範 SSRF 等安全風險。

**Why this priority**: 屬於非功能性需求，但對產品的穩定運行與安全性至關重要。在核心功能完成後必須實作，才能正式上線。

**Independent Test**: 可透過模擬大量連結檢測來驗證並行數量限制、同網域限制是否生效，以及確認私有 IP 位址被正確拒絕。

**Acceptance Scenarios**:

1. **Given** 檢測中有多個連結指向同一網域, **When** 系統發送請求, **Then** 同一時間對該網域最多發送 3 個並行請求
2. **Given** 檢測中有大量連結, **When** 系統發送請求, **Then** 系統整體同時最多發送 20 個 HTTP 請求
3. **Given** 某個連結指向私有 IP 位址（如 127.0.0.1、10.x.x.x、192.168.x.x）, **When** 系統嘗試驗證, **Then** 該請求被阻擋，連結標記為 ❌ Broken 並附上原因「禁止存取私有位址」
4. **Given** 同一來源 IP 在一分鐘內已發起 5 次檢測, **When** 嘗試第 6 次檢測, **Then** 系統回傳友善提示「請求過於頻繁，請稍後再試」
5. **Given** 檢測中某個連結回應 HTTP 429 Too Many Requests, **When** 系統處理回應, **Then** 該連結標記為 ⚠️ Warning 並附上原因「目標站點速率限制」

---

### Edge Cases

- 使用者輸入的 Markdown 完全不包含任何連結時，系統應顯示「未找到任何連結」的友善提示
- 使用者輸入空白內容並點選「開始檢測」時，系統應提示「請輸入 Markdown 內容」
- 目標伺服器因 HEAD 請求回傳 405 Method Not Allowed 時，系統應自動改用 GET 請求重試
- 連結目標產生超過 5 次重導向時，系統應停止追蹤並標記為 ⚠️ Warning
- 單次檢測的連結總數超過 5,000 個時，系統應提示已達上限並僅檢查前 5,000 個
- HTML 註解 `<!-- ... -->` 內的連結不應被檢查
- Inline code（反引號包裹）內的 URL 不應被檢查
- Markdown 中純文字的 URL-like 字串（未使用 `[text](url)` 或 `<url>` 語法）不應被檢查
- 同一個外部 URL 出現在多個檔案中時，僅發送一次 HTTP 請求，結果共用
- 單一檔案包含超過 1,000 個連結時，僅解析前 1,000 個並提示使用者
- GitHub API Rate Limit 耗盡時（未認證 60 次/小時），顯示友善提示告知使用者
- `mailto:` 連結僅驗證格式合法性，不發送實際郵件

## Requirements *(mandatory)*

### Functional Requirements

**輸入與模式**

- **FR-001**: 系統 MUST 提供兩種檢測模式：「Repo URL」模式與「Markdown 原始碼」模式
- **FR-002**: 在 Repo URL 模式下，系統 MUST 接受格式為 `https://github.com/{owner}/{repo}` 的 GitHub Repository URL
- **FR-003**: 在 Repo URL 模式下，系統 MUST 驗證 URL 格式合法性，不合法時顯示友善錯誤訊息
- **FR-004**: 在 Repo URL 模式下，系統 MUST 僅支援公開（Public）Repository，私有 Repo 應顯示友善提示
- **FR-005**: 在 Repo URL 模式下，系統 SHOULD 允許使用者指定分支名稱，未指定時使用 Repo 預設分支
- **FR-006**: 在 Markdown 原始碼模式下，系統 MUST 接受使用者直接貼上的 Markdown 文字
- **FR-007**: 在 Markdown 原始碼模式下，系統 MUST 限制輸入上限為 100,000 字元，超過時顯示友善提示

**Repo 掃描**

- **FR-008**: 系統 MUST 遞迴掃描 Repo 中所有目錄的 `.md` 副檔名檔案
- **FR-009**: 系統 MUST 限制單一 Repo 最多掃描 500 個 `.md` 檔案，超過時提示使用者
- **FR-010**: 系統 MUST NOT 掃描 `.txt`、`.rst` 等其他格式檔案

**連結解析**

- **FR-011**: 系統 MUST 解析 Markdown 中使用 `[text](url)` 語法的連結
- **FR-011a**: 系統 MUST 解析 Markdown 中使用 reference-style 語法的連結（如 `[text][ref]` 搭配 `[ref]: url` 定義），與 inline 連結同等處理
- **FR-012**: 系統 MUST 解析 Markdown 中使用 `<url>` 語法的自動連結
- **FR-013**: 系統 MUST 解析 Markdown 中的圖片連結 `![alt](url)`
- **FR-014**: 系統 MUST 忽略 fenced code block（` ``` `）內的所有連結
- **FR-015**: 系統 MUST 忽略 inline code（反引號）內的所有連結
- **FR-016**: 系統 MUST 忽略 HTML 註解 `<!-- ... -->` 內的所有連結
- **FR-017**: 系統 MUST NOT 檢查未使用 Markdown 連結語法標記的純文字 URL
- **FR-018**: 系統 MUST 限制單一檔案最多解析 1,000 個連結

**連結驗證**

- **FR-019**: 系統 MUST 對外部 URL（HTTP/HTTPS）發送 HTTP HEAD 請求驗證有效性
- **FR-019a**: 系統 MUST NOT 驗證外部 URL 的片段識別符（fragment / anchor），僅檢查 HTTP 回應狀態
- **FR-020**: 當 HEAD 請求被拒絕（HTTP 405）時，系統 MUST 改用 GET 請求重試
- **FR-021**: 系統 MUST 對 HTTP 2xx 回應標記為 ✅ Healthy
- **FR-021a**: 系統 MUST 對經 HTTP 301 永久重導向後最終到達 2xx 的連結標記為 ⚠️ Warning，並在報告中顯示重導向後的新 URL，提示使用者更新連結
- **FR-021b**: 系統 MUST 對經 HTTP 302 暫時重導向後最終到達 2xx 的連結標記為 ✅ Healthy
- **FR-022**: 系統 MUST 對 HTTP 4xx/5xx 回應標記為 ❌ Broken
- **FR-023**: 系統 MUST 對逾時（超過 10 秒）、過多重導向（超過 5 次）、HTTP 429 回應標記為 ⚠️ Warning
- **FR-024**: 系統 MUST 對 Repo 內相對路徑連結，透過確認檔案是否存在來驗證
- **FR-025**: 系統 MUST 對錨點連結，解析目標檔案的標題確認錨點是否存在
- **FR-026**: 系統 MUST 對 `mailto:` 連結僅驗證格式合法性
- **FR-027**: 系統 MUST 對逾時或 5xx 錯誤最多重試 1 次
- **FR-028**: 系統 MUST 對多個檔案中出現的相同外部 URL 合併為一次 HTTP 請求，結果共用
- **FR-029**: 在 Markdown 原始碼模式下，系統 MUST 將相對路徑連結標記為 ⏭️ Skipped，並附上原因「缺少 Repo 上下文」

**錨點拼字建議**

- **FR-030**: 當錨點連結失效時，系統 SHOULD 對比該檔案中所有實際標題產生的錨點，若存在編輯距離 ≤ 2 的候選項，則在報告中提示「did you mean #xxx?」

**速率控制與安全**

- **FR-031**: 系統 MUST 對同一網域最多同時發送 3 個並行請求
- **FR-032**: 系統 MUST 整體同時最多發送 20 個 HTTP 請求
- **FR-033**: 系統 MUST 遵守 GitHub API Rate Limit，超過時顯示友善提示
- **FR-034**: 系統 MUST 禁止存取私有 IP 位址（127.0.0.0/8、10.0.0.0/8、172.16.0.0/12、192.168.0.0/16）及 localhost，防範 SSRF
- **FR-035**: 系統 MUST 限制單次檢測最多 5,000 個連結
- **FR-036**: 系統 MUST 限制同一來源 IP 每分鐘最多 5 次檢測請求
- **FR-037**: 系統 MUST 在 HTTP 請求中攜帶可辨識的 User-Agent 標頭（如 `MarkdownLinkCheck/1.0`）

**使用者體驗**

- **FR-038**: 系統 MUST 在檢測過程中透過 Server-Sent Events (SSE) 即時推送進度「已檢查 N / 共 M 個連結」
- **FR-039**: 系統 MUST 每完成一個檔案的檢查即透過 SSE 串流該檔案的結果，不需等全部完成
- **FR-040**: 系統 MUST 在報告完成後顯示總耗時
- **FR-041**: 系統 MUST 在報告中將 Broken 排最前、Warning 次之、Healthy 摘要放最後
- **FR-042**: 系統 MUST 提供「複製為 Markdown」按鈕，將報告以 Markdown 格式複製到剪貼簿
- **FR-043**: 系統 MUST 在報告中依檔案分組，每個連結標示行號與錯誤類型

### Key Entities

- **CheckRequest（檢測請求）**: 代表使用者發起的一次連結檢測。屬性包含：檢測模式（Repo URL / Markdown 原始碼）、輸入內容（URL 或 Markdown 文字）、指定分支（可選）、發起時間、來源 IP
- **MarkdownFile（Markdown 檔案）**: 代表一個被掃描的 Markdown 檔案。屬性包含：檔案名稱、相對路徑、檔案內容。屬於一個 CheckRequest
- **Link（連結）**: 代表從 Markdown 中解析出的一個連結。屬性包含：連結類型（外部 URL / 相對路徑 / 錨點 / 郵件 / 圖片）、原始文字、目標 URL、所在行號。屬於一個 MarkdownFile
- **LinkResult（連結檢測結果）**: 代表一個連結的驗證結果。屬性包含：狀態（Healthy / Broken / Warning / Skipped）、HTTP 狀態碼（若適用）、錯誤類型、錯誤訊息、拼字建議（若適用）。對應一個 Link
- **CheckReport（檢測報告）**: 代表一次檢測的最終輸出。屬性包含：掃描檔案數、檢查連結數、各狀態計數、總耗時。包含多個 MarkdownFile 及其 LinkResult

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 使用者從貼上 Markdown 原始碼到看見完整檢測報告，整個操作流程可在 30 秒內完成（以 50 個連結為基準）
- **SC-002**: 使用者從輸入 GitHub Repo URL 到看見完整檢測報告，整個操作流程可在 2 分鐘內完成（以 10 個 .md 檔案、200 個連結為基準）
- **SC-003**: 系統對已知失效連結（HTTP 404）的檢測準確率達 99% 以上
- **SC-004**: 系統對 Code Block 內連結的忽略準確率達 100%（零誤判）
- **SC-005**: 首次使用者從首頁載入到完成一次 Markdown 原始碼模式檢測，操作步驟不超過 3 步（選擇模式 → 貼上內容 → 點選開始檢測），且所有操作入口均可在不捲動頁面的情況下看見（above the fold）
- **SC-006**: 在即時串流模式下，使用者在第一個檔案檢測完成後 2 秒內即可看到該檔案結果
- **SC-007**: 系統在面對大量相同網域連結時不觸發目標站點的速率限制封鎖
- **SC-008**: 錨點拼字建議功能在編輯距離 ≤ 2 的情況下能正確提示替代錨點

## Clarifications

### Session 2026-02-23

- Q: 系統是否應該解析 reference-style Markdown 連結（如 `[text][ref]` 搭配 `[ref]: url`）？ → A: 是，支援 reference-style 連結，與 inline 連結同等處理
- Q: 即時串流機制應採用哪種技術？ → A: 採用 Server-Sent Events (SSE)，單向推送、輕量且瀏覽器原生支援
- Q: 對於外部 URL 的片段識別符（fragment），是否應驗證外部頁面錨點存在性？ → A: 不檢查，僅驗證 HTTP 回應狀態
- Q: UI 的主要語言方向？ → A: 中文為主，狀態標籤（Healthy/Broken/Warning/Skipped）保留英文
- Q: HTTP 301 永久重導向後成功到達目的地，應標記為 Healthy 還是 Warning？ → A: 301=Warning 並顯示新 URL；302=Healthy

## Assumptions

- UI 語言以中文為主，狀態標籤（Healthy / Broken / Warning / Skipped）保留英文以利在報告貼入 GitHub Issue/PR 時保持讀性

- 本 MVP 階段僅支援 GitHub 平台的公開 Repository，不支援 GitLab、Bitbucket 等其他平台
- 歷史報告查詢功能不在本 MVP 範圍內
- 未認證的 GitHub API 存取限制為 60 次/小時，系統不要求使用者提供 GitHub Token（MVP 階段）
- 連結驗證以 HTTP HEAD 請求為優先，僅在 HEAD 被拒絕時才發送 GET 請求
- 系統不需要使用者註冊或登入即可使用
- 報告不會被持久化儲存，使用者需自行複製保存（MVP 階段）
