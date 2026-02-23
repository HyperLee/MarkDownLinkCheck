---
name: playwright-skill
description: Complete browser automation with Playwright. Auto-detects dev servers, writes clean test scripts to /tmp. Test pages, fill forms, take screenshots, check responsive design, validate UX, test login flows, check links, automate any browser task. Use when user wants to test websites, automate browser interactions, validate web functionality, or perform any browser-based testing.
---

**重要路徑說明：**
此技能可以安裝在不同位置（外掛系統、手動安裝、全域或專案特定）。在執行任何指令之前，請根據您載入此 SKILL.md 檔案的位置確定技能目錄，並在後續所有指令中使用該路徑。將 `$SKILL_DIR` 替換為實際發現的路徑。

# Playwright 瀏覽器自動化技能

這是一個全功能的瀏覽器自動化技能。我會根據您的需求撰寫自定義的 Playwright 程式碼，並透過通用執行器執行。

**核心工作流程 - 請依序遵循以下步驟：**

1. **自動偵測開發伺服器** - 針對本地測試，請務必先執行伺服器偵測：

   ```bash
   cd $SKILL_DIR && node -e "require('./lib/helpers').detectDevServers().then(servers => console.log(JSON.stringify(servers)))"
   ```

2. **強制定義測試計畫 (Test Plan)** - 每個測試腳本**必須**在頂部定義測試計畫，以便在報告中呈現：
   ```javascript
   helpers.initTestPlan({
     purpose: '測試目的',
     workflow: '1. 進入首頁 -> 2. 登入 -> 3. 驗證狀態',
     behaviors: '驗證登入成功後是否顯示使用者名稱'
   });
   ```

3. **撰寫腳本至 temp_scripts** - 優先將測試腳本寫入 `temp_scripts/playwright-test-*.js`，方便後續維護與追蹤。
   - **務必包含截圖**：在關鍵步驟（如登入成功、頁面載入後、流程結束前）呼叫 `helpers.takeScreenshot(page, '描述')`。
   - **預設開啟錄影**：使用 `helpers.createContext(browser)` 會自動啟用錄影功能。

4. **預設使用無頭模式** - 除非特別要求可見模式（headed），否則請預設使用 `headless: true`。

5. **參數化網址** - 務必將網址設為腳本頂部的常數或環境變數。

## 運作方式

1. 您描述想要測試或自動化的任務。
2. 我會偵測執行中的開發伺服器（或詢問外部網站網址）。
3. 我會撰寫自定義程式碼至 `temp_scripts/playwright-test-*.js`。
4. 我會執行腳本：`cd $SKILL_DIR && node run.js temp_scripts/playwright-test-*.js`。
5. **自動生成報告**：執行完成後，會在當前目錄生成 `playwright-report-media/report.html`，包含截圖與錄影。

## 快速開始（首次設定）

```bash
cd $SKILL_DIR
npm run setup
```

## 執行範例

**步驟 1：偵測開發伺服器**

```bash
cd $SKILL_DIR && node -e "require('./lib/helpers').detectDevServers().then(s => console.log(JSON.stringify(s)))"
```

**步驟 2：撰寫並執行測試**

```javascript
// temp_scripts/playwright-test-page.js
// 1. 初始化測試計畫（強制性）
initTestPlan({
  purpose: '驗證 Google 搜尋功能',
  workflow: '進入首頁 -> 截圖存證',
  behaviors: '頁面載入正常且可見'
});

const browser = await helpers.launchBrowser();
const context = await helpers.createContext(browser);
const page = await helpers.createPage(context);

try {
  await page.goto('https://www.google.com.tw');
  
  // 2. 記錄步驟
  logStep('進入 Google 首頁', true, { behavior: '開啟瀏覽器並導向網址' });
  
  await helpers.takeScreenshot(page, 'google-home');
} finally {
  await browser.close();
}
// 執行結束後會自動生成 HTML 報告
```

## 測試憲法與總則 (Test Constitution)

> **導讀**：本憲法為最高原則。具體的技術實作細節與進階模式，請參閱 [API_REFERENCE.md](API_REFERENCE.md)。

為了確保腳本的穩定性與偵錯效率，請務必遵循以下核心原則：

1. **穩定性優於速度 (Stability Over Speed)**：
   - **水合等待 (Hydration)**：導航至頁面後，優先使用 `expect(page.getByTestId(...)).toBeVisible({ timeout: 15000 })` 確保關鍵元件已掛載。
   - **緩衝時間**：在關鍵操作後，視情況加入 `await page.waitForTimeout(2000)` 以確保 DOM 狀態完全穩定。
   - **智慧等待**：優先使用 `waitForSelector` 或 `waitForLoadState('domcontentloaded')`，避免依賴不穩定的 `networkidle`。

2. **偵錯透明化 (Transparent Debugging)**：
   - **日誌捕捉**：腳本執行期間，瀏覽器端的 `console.log/warn/error` 已由 `helpers.createPage` 自動攔截並輸出至 Terminal。
   - **鼓勵 Log**：開發者應主動在 `page.evaluate()` 中加入 `console.log` 來觀察網頁內部行為。

3. **反應性與同步 (Reactivity & Sync)**：
    - 在執行任何改變頁面狀態的操作（如點擊過濾、捲動載入）後，應確保下一個步驟前頁面已完成狀態同步。

4. **有狀態流程與錯誤阻斷 (Stateful Flow & Early Exit)**：
   - **順序依賴 (Serial Thinking)**：對於「登入 -> 搜尋 -> 擷取」這類有前後依賴關係的流程，應視為一個單一的序列。
   - **及早失敗 (Fail Fast)**：一旦關鍵步驟（如登入）失敗，應立即停止後續操作並回報錯誤，避免產生連鎖反應的無效日誌與截圖。
   - **狀態累積驗證**：在同一個頁面生命週期內執行連續操作，以驗證狀態轉換（例如：從 GFM 切換回標準模式時，內容是否保持一致）。

## 撰寫規範 (Code Standards)

為了確保測試腳本能正確執行並產生完整的 HTML 報告，請遵循以下規範：

### 0. 語法穩定性原則 (Syntax Stability) - **重要**
- **禁止行末反斜線**：嚴禁在 JavaScript 程式碼行末使用反斜線 `\` 進行換行。這會導致轉譯錯誤。
- **使用標準樣板字面值**：多行字串請務必使用反引號 (Backticks `` ` ``)，而非使用 `+` 號或反斜線拼接。
- **簡潔明瞭**：保持程式碼結構清晰，避免過度複雜的巢狀結構。

### 1. 簡潔模式 (Zero-Config Mode) - **推薦使用**
當腳本中**不包含** `require(` 且**不包含** `(async () => {` 時，執行器會自動注入全域工具並封裝非同步環境。

- **自動注入的全域變數**：
    - `helpers`: 核心工具函式庫（包含 `launchBrowser`, `takeScreenshot` 等）。
    - `initTestPlan(plan)`: 初始化測試計畫（用於報告標題與步驟說明）。
    - `logStep(name, success, options)`: 記錄執行步驟。
    - `getContextOptionsWithHeaders(options)`: 取得預設的瀏覽器上下文配置。
- **寫法示例**：
    ```javascript
    // 不需要 require，不需要 async 封裝，直接寫 await 邏輯
    initTestPlan({
      purpose: '驗證登入功能',
      workflow: '1. 輸入帳密 -> 2. 點擊登入',
      behaviors: '登入後應看到儀表板'
    });

    const browser = await helpers.launchBrowser();
    const context = await browser.newContext(getContextOptionsWithHeaders());
    const page = await browser.newPage();

    try {
      logStep('正在開啟頁面');
      await page.goto('https://example.com');
      await helpers.takeScreenshot(page, 'login-page');
      logStep('頁面載入成功', true);
    } finally {
      await browser.close();
    }
    ```
- **進階技巧**：若需使用 Node.js 內建模組（如 `fs`, `path`），請在 `require` 與括號間**加上空格**（例如 `const fs = require ('fs');`），即可避開執行器的 Full-Control 偵測，維持簡潔模式。

### 2. 完全控制模式 (Full-Control Mode)
當腳本中**包含** `require(` 或 `(async () => {` 時，執行器會認定這是一個獨立腳本，**不會**注入任何全域變數。

- **必須手動處理**：
    - 需要自行 `require('playwright')`。
    - 需要自行 `require` 工具類別：`const helpers = require(path.join(process.cwd(), 'lib/helpers'))`。
    - 必須自行處理 `async/await` 封裝。
- **適用場景**：需要引入額外的 npm 套件，或是有複雜的模組化結構時。

---

## 核心工具函式庫 (lib/helpers.js) 常用方法

- `helpers.launchBrowser()`: 啟動預設配置的瀏覽器。
- `helpers.createContext(browser)`: 建立支援錄影與自定義標頭的上下文。
- `helpers.createPage(context)`: 建立新頁面。
- `helpers.takeScreenshot(page, name)`: 儲存帶有時間戳記的截圖。
- `helpers.safeClick(page, selector)`: 帶有重試機制的安全點擊。
- `helpers.safeType(page, selector, text)`: 安全的文字輸入。
- `helpers.handleCookieBanner(page)`: 自動處理常見的 Cookie 同意橫幅。
- `helpers.logStep(name, success, options)`: 記錄測試步驟，支援傳入 `{ behavior: '描述行為', reason: '測試理由' }` 以對齊報告中的追蹤資訊。
- `helpers.currentTestPlan`: 當前生效的測試計畫物件。
- `helpers.stats.steps`: 包含所有步驟細節的陣列，自動統計過測與失敗數量。

## 報告功能亮點
- **📊 視覺化報告**：自動生成包含截圖與錄影的 HTML 報告。
- **📝 測試計畫顯示**：報告頂部清楚展示測試目的、流程與預期行為。
- **📈 多步驟統計與分類**：自動計算成功與失敗的步驟數量，並在報告中分開列出詳細清單，一目了然。
- **🤖 AI 深度分析**：整合 AI 結論區塊，提供專業的測試總結（支援 Markdown 格式）。
- **🔍 錯誤原因探究 (Root Cause Analysis)**：測試失敗時自動分析錯誤類型（逾時、選擇器失效、邏輯錯誤等），並給予具體的修復建議與出錯位置記錄。
- **📂 自動分類管理**：每次執行自動建立時間戳記資料夾，避免報告覆蓋。
- **🔗 最新報告捷徑**：永遠可以透過 `playwright-reports/latest-report.html` 快速開啟最近一次的結果。

## 快速開始
```bash
# 執行檔案
node run.js your-script.js

# 執行內嵌程式碼
node run.js "const browser = await helpers.launchBrowser(); ..."
```

## 提示

- **重要：優先偵測伺服器** - 在為 localhost 撰寫測試程式碼之前，請務必先執行 `detectDevServers()`。
- **自定義標頭** - 使用 `PW_HEADER_NAME`/`PW_HEADER_VALUE` 環境變數來識別發往後端的自動化流量。
- **使用 temp_scripts 存放測試檔案** - 寫入 `temp_scripts/playwright-test-*.js`，確保腳本可被追蹤且不遺失。
- **參數化網址** - 在每個腳本頂部的 `TARGET_URL` 常數中放置偵測到或提供的網址。
- **預設：無頭模式** - 除非使用者明確要求「可見」或「視窗」執行，否則請使用 `headless: true`。
- **等待策略** - 使用 `waitForURL`、`waitForSelector`、`waitForLoadState` 而非固定的等待時間。
- **錯誤處理** - 務必使用 try-catch 以確保自動化流程的強韌性。
- **媒體資源與計畫強制要求**：AI 產出的程式碼**必須**包含：
  1. 呼叫 `helpers.initTestPlan({...})` 定義計畫。
  2. 至少一張截圖 `helpers.takeScreenshot`。
  3. 使用 `helpers.createContext` 以確保有錄影存證。
  這是為了讓使用者在查看報告時有完整的測試背景與視覺證據。
