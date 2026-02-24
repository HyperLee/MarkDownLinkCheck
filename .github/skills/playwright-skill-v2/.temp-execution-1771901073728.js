
const { chromium, firefox, webkit, devices } = require('playwright');
const path = require('path');
const helpers = require('C:/GitHubFolder/MarkDownLinkCheck/.github/skills/playwright-skill-v2/lib/helpers');

// 測試計畫環境變數
process.env.PW_TEST_PLAN = "";

// Extra headers from environment variables (if configured)
const __extraHeaders = helpers.getExtraHeadersFromEnv();

/**
 * Utility to merge environment headers into context options.
 * Also enables video and screenshot recording for the report.
 * @param {Object} options - Context options
 * @returns {Object} Options with extraHTTPHeaders and recording options merged in
 */
function getContextOptionsWithHeaders(options = {}) {
  const reportDir = process.env.PW_REPORT_DIR || path.join(process.cwd(), 'playwright-reports');
  
  return {
    ...options,
    recordVideo: options.recordVideo || {
      dir: path.join(reportDir, 'videos/'),
      size: { width: 1280, height: 720 }
    },
    extraHTTPHeaders: {
      ...__extraHeaders,
      ...(options.extraHTTPHeaders || {})
    }
  };
}

// 將執行邏輯封裝並匯出，讓 run.js 可以 await 它
module.exports = async function runTest() {
  const startTime = Date.now();
  let status = '成功';
  let errorAnalysis = null;
  let pageContext = { url: 'N/A', title: 'N/A', statusCode: 'N/A' };
  
  // 使用 helpers 內建的統計與測試計畫
  const stats = helpers.stats;

  // 注入 helpers 核心功能到全域
  global.helpers = helpers;

  // 重要：建立全域參考，讓 helpers.js 與 run.js 共享同一個狀態物件
  helpers.setGlobalStats(stats);
  
  // 注入 initTestPlan 到全域，確保調用的是同一個實例
  global.initTestPlan = (plan) => helpers.initTestPlan(plan);
  global.getContextOptionsWithHeaders = getContextOptionsWithHeaders;
  
  const logStep = (name, success = true, options = {}) => {
    helpers.logStep(name, success, options);
    if (!success) status = '失敗';
  };
  global.logStep = logStep;
  global.stats = stats;

    // 解析測試計畫
    let testPlan = helpers.currentTestPlan; // 預設使用 helpers 中已初始化的計畫
    try {
      if (process.env.PW_TEST_PLAN) {
        // 如果有環境變數，優先使用環境變數（這通常發生在外部傳入計畫時）
        testPlan = JSON.parse(process.env.PW_TEST_PLAN);
      }
    } catch (e) {
      // 如果解析失敗，嘗試處理轉義的換行符
      try {
        const rawPlan = process.env.PW_TEST_PLAN.replace(/\\n/g, '\n');
        testPlan = JSON.parse(rawPlan);
      } catch (e2) {
        // 仍然失敗則使用 helpers 預設值
      }
    }

  // 取得當前執行的原始程式碼
  const rawTestCode = "﻿// Playwright 測試腳本：Markdown Link Check 網站全面檢測\r\n// 目標：http://localhost:5184\r\n\r\nconst TARGET_URL = 'http://localhost:5184';\r\n\r\ninitTestPlan({\r\n  purpose: '全面檢測 Markdown Link Check 網站的 UI 與核心功能',\r\n  workflow: '1. 首頁載入 -> 2. UI 元素檢查 -> 3. 模式切換 -> 4. 輸入驗證 -> 5. Markdown 檢測流程 -> 6. 報告驗證 -> 7. RWD 響應式測試',\r\n  behaviors: '驗證首頁正確載入、模式切換正常、表單驗證生效、Markdown 連結檢測流程完整運作、檢測報告正確顯示、響應式設計適配'\r\n});\r\n\r\nconst browser = await helpers.launchBrowser();\r\nconst context = await helpers.createContext(browser);\r\nconst page = await helpers.createPage(context);\r\n\r\ntry {\r\n  // ===== 步驟 1: 首頁載入 =====\r\n  await page.goto(TARGET_URL, { waitUntil: 'domcontentloaded', timeout: 15000 });\r\n  await page.waitForTimeout(2000);\r\n  \r\n  const title = await page.title();\r\n  logStep('首頁載入成功', true, { behavior: `頁面標題: ${title}` });\r\n  await helpers.takeScreenshot(page, '01-homepage-loaded');\r\n\r\n  // ===== 步驟 2: 驗證主要 UI 元素存在 =====\r\n  const h1 = page.locator('h1');\r\n  const h1Text = await h1.textContent();\r\n  logStep('主標題顯示正確', h1Text.includes('Markdown Link Check'), { \r\n    behavior: `標題內容: \"${h1Text}\"` \r\n  });\r\n\r\n  // 檢查模式選擇區域\r\n  const modeMarkdown = page.locator('#modeMarkdown');\r\n  const modeRepo = page.locator('#modeRepo');\r\n  const isMarkdownChecked = await modeMarkdown.isChecked();\r\n  logStep('預設模式為 Markdown 原始碼', isMarkdownChecked, { \r\n    behavior: 'Markdown 原始碼 radio 預設被選取' \r\n  });\r\n\r\n  // 檢查 textarea 存在\r\n  const textarea = page.locator('#markdownContent');\r\n  const textareaVisible = await textarea.isVisible();\r\n  logStep('Markdown 輸入區顯示', textareaVisible, { \r\n    behavior: 'textarea 可見' \r\n  });\r\n\r\n  // 檢查開始按鈕存在\r\n  const startButton = page.locator('#startCheck');\r\n  const startButtonVisible = await startButton.isVisible();\r\n  logStep('「開始檢測」按鈕顯示', startButtonVisible, { \r\n    behavior: '按鈕可見且可互動' \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '02-ui-elements-verified');\r\n\r\n  // ===== 步驟 3: 模式切換測試 =====\r\n  // 切換到 GitHub Repo 模式\r\n  await modeRepo.click();\r\n  await page.waitForTimeout(500);\r\n\r\n  const repoSection = page.locator('#repoUrlSection');\r\n  const repoVisible = await repoSection.isVisible();\r\n  logStep('切換到 GitHub Repo 模式', repoVisible, { \r\n    behavior: 'Repo URL 輸入區塊顯示' \r\n  });\r\n\r\n  const markdownSection = page.locator('#markdownSourceSection');\r\n  const markdownHidden = !(await markdownSection.isVisible());\r\n  logStep('Markdown 輸入區隱藏', markdownHidden, { \r\n    behavior: 'Markdown 模式的 textarea 被隱藏' \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '03-repo-mode');\r\n\r\n  // 切回 Markdown 模式\r\n  await modeMarkdown.click();\r\n  await page.waitForTimeout(500);\r\n\r\n  const markdownVisibleAgain = await markdownSection.isVisible();\r\n  logStep('切回 Markdown 模式', markdownVisibleAgain, { \r\n    behavior: 'Markdown textarea 重新顯示' \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '04-back-to-markdown-mode');\r\n\r\n  // ===== 步驟 4: 空白內容輸入驗證 =====\r\n  // 不填入任何內容，直接點擊開始\r\n  await startButton.click();\r\n  await page.waitForTimeout(1000);\r\n\r\n  const errorMsg = page.locator('#errorMessage');\r\n  const errorVisible = await errorMsg.isVisible();\r\n  let errorText = '';\r\n  if (errorVisible) {\r\n    errorText = await errorMsg.textContent();\r\n  }\r\n  logStep('空白內容驗證', errorVisible && errorText.includes('請輸入'), { \r\n    behavior: `錯誤訊息: \"${errorText}\"` \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '05-empty-validation');\r\n\r\n  // ===== 步驟 5: 字元計數器測試 =====\r\n  const testText = '# 測試標題\\n這是一段測試文字';\r\n  await textarea.fill(testText);\r\n  await page.waitForTimeout(500);\r\n  \r\n  const charCountEl = page.locator('#charCount');\r\n  const charCountText = await charCountEl.textContent();\r\n  const expectedCount = testText.length;\r\n  logStep('字元計數器正常', charCountText === String(expectedCount), { \r\n    behavior: `輸入 ${expectedCount} 字元，顯示 ${charCountText}` \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '06-char-counter');\r\n\r\n  // ===== 步驟 6: 執行 Markdown 連結檢測（核心功能）=====\r\n  // 監聽瀏覽器 console 錯誤\r\n  const browserErrors = [];\r\n  page.on('console', msg => {\r\n    if (msg.type() === 'error') {\r\n      browserErrors.push(msg.text());\r\n      console.log(`  [Browser Error] ${msg.text()}`);\r\n    }\r\n  });\r\n\r\n  // 監聽網路回應\r\n  page.on('response', response => {\r\n    if (response.url().includes('/api/check/sse')) {\r\n      console.log(`  [Network] SSE Response status: ${response.status()}`);\r\n    }\r\n  });\r\n\r\n  // 準備含有連結的 Markdown 內容\r\n  const markdownWithLinks = `# Link Check Test\r\n\r\nThis is a test document with various links:\r\n\r\n- [Google](https://www.google.com)\r\n- [GitHub](https://github.com)\r\n- [Broken Link](https://this-domain-definitely-does-not-exist-xyz123.com)\r\n- [Anchor link](#link-check-test)\r\n`;\r\n\r\n  await textarea.fill(markdownWithLinks);\r\n  await page.waitForTimeout(500);\r\n  \r\n  logStep('填入含連結的 Markdown 內容', true, { \r\n    behavior: '含 3 個外部連結和 1 個錨點連結' \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '07-markdown-with-links');\r\n\r\n  // 點擊開始檢測\r\n  await startButton.click();\r\n  logStep('點擊「開始檢測」', true, { behavior: '觸發 SSE 連結檢測流程' });\r\n\r\n  // 等待進度區出現\r\n  const progressSection = page.locator('#progressSection');\r\n  try {\r\n    await progressSection.waitFor({ state: 'visible', timeout: 10000 });\r\n    logStep('進度列顯示', true, { behavior: '檢測進度區塊出現' });\r\n    await helpers.takeScreenshot(page, '08-progress-showing');\r\n  } catch (e) {\r\n    logStep('進度列顯示', false, { behavior: '進度區塊未出現', reason: e.message });\r\n    await helpers.takeScreenshot(page, '08-progress-error');\r\n  }\r\n\r\n  // 等待取消按鈕出現\r\n  const cancelButton = page.locator('#cancelCheck');\r\n  const cancelVisible = await cancelButton.isVisible();\r\n  logStep('取消按鈕顯示', cancelVisible, { \r\n    behavior: '檢測期間取消按鈕可見' \r\n  });\r\n\r\n  // 等待檢測完成 - 使用 waitForFunction 監控 DOM 變化\r\n  const reportSummary = page.locator('#reportSummary');\r\n  try {\r\n    // 方法 1: 等待 fileResults 出現內容或 reportSummary 顯示\r\n    await page.waitForFunction(() => {\r\n      const reportEl = document.getElementById('reportSummary');\r\n      const fileResults = document.getElementById('fileResults');\r\n      return (reportEl && reportEl.style.display === 'block') || \r\n             (fileResults && fileResults.children.length > 0);\r\n    }, { timeout: 30000 });\r\n\r\n    // 等 DOM 完全更新\r\n    await page.waitForTimeout(2000);\r\n    \r\n    const reportVisible = await reportSummary.isVisible();\r\n    const fileCardsNow = await page.locator('#fileResults .card').count();\r\n    logStep('檢測完成', reportVisible || fileCardsNow > 0, { \r\n      behavior: `報告摘要可見: ${reportVisible}, 檔案卡片數: ${fileCardsNow}` \r\n    });\r\n    await helpers.takeScreenshot(page, '09-report-summary');\r\n  } catch (e) {\r\n    // 額外除錯：讀取頁面內部狀態\r\n    const debugInfo = await page.evaluate(() => {\r\n      const reportEl = document.getElementById('reportSummary');\r\n      const fileResults = document.getElementById('fileResults');\r\n      const errorMsg = document.getElementById('errorMessage');\r\n      const progressText = document.getElementById('progressText');\r\n      return {\r\n        reportDisplay: reportEl?.style?.display,\r\n        fileResultsHTML: fileResults?.innerHTML?.substring(0, 500),\r\n        errorDisplay: errorMsg?.style?.display,\r\n        errorText: errorMsg?.textContent,\r\n        progressText: progressText?.textContent\r\n      };\r\n    });\r\n    console.log('  [Debug] 頁面狀態:', JSON.stringify(debugInfo, null, 2));\r\n    console.log('  [Debug] 瀏覽器錯誤:', browserErrors);\r\n    \r\n    logStep('檢測完成，報告摘要顯示', false, { \r\n      behavior: `報告摘要未出現 (display: ${debugInfo.reportDisplay})`, \r\n      reason: `進度: ${debugInfo.progressText}, 錯誤: ${debugInfo.errorText || '無'}` \r\n    });\r\n    await helpers.takeScreenshot(page, '09-report-timeout');\r\n  }\r\n\r\n  // ===== 步驟 7: 驗證報告內容 =====\r\n  // 檢查檔案結果是否有出現\r\n  const fileResults = page.locator('#fileResults .card');\r\n  const fileResultCount = await fileResults.count();\r\n  logStep('檔案結果卡片顯示', fileResultCount > 0, { \r\n    behavior: `共 ${fileResultCount} 個檔案結果卡片` \r\n  });\r\n\r\n  // 檢查統計數據\r\n  const totalLinks = page.locator('#summaryTotal');\r\n  const totalText = await totalLinks.textContent();\r\n  logStep('連結總數統計', parseInt(totalText) > 0, { \r\n    behavior: `檢測到 ${totalText} 個連結` \r\n  });\r\n\r\n  const healthyCount = page.locator('#summaryHealthy');\r\n  const healthyText = await healthyCount.textContent();\r\n  logStep('正常連結數量統計', true, { \r\n    behavior: `正常連結: ${healthyText}` \r\n  });\r\n\r\n  const brokenCount = page.locator('#summaryBroken');\r\n  const brokenText = await brokenCount.textContent();\r\n  logStep('失效連結數量統計', true, { \r\n    behavior: `失效連結: ${brokenText}` \r\n  });\r\n\r\n  // 檢查「複製為 Markdown」按鈕\r\n  const copyButton = page.locator('#copyMarkdown');\r\n  const copyBtnVisible = await copyButton.isVisible();\r\n  logStep('「複製為 Markdown」按鈕顯示', copyBtnVisible, { \r\n    behavior: '報告中可見複製按鈕' \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '10-report-details');\r\n\r\n  // ===== 步驟 8: 驗證連結結果表格 =====\r\n  const resultRows = page.locator('#fileResults table tbody tr');\r\n  const rowCount = await resultRows.count();\r\n  logStep('連結結果列表', rowCount > 0, { \r\n    behavior: `表格中共 ${rowCount} 列結果` \r\n  });\r\n\r\n  // 檢查狀態圖標（✅ 或 ❌）\r\n  const statusIcons = page.locator('#fileResults table tbody tr td:first-child');\r\n  let hasStatusIcons = false;\r\n  if (await statusIcons.count() > 0) {\r\n    const firstIcon = await statusIcons.first().textContent();\r\n    hasStatusIcons = firstIcon.includes('✅') || firstIcon.includes('❌') || firstIcon.includes('⚠️') || firstIcon.includes('⏭️');\r\n  }\r\n  logStep('連結狀態圖標顯示', hasStatusIcons, { \r\n    behavior: '每列有狀態圖標（✅/❌/⚠️/⏭️）' \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '11-link-results-table');\r\n\r\n  // ===== 步驟 9: GitHub Repo 模式驗證 =====\r\n  // 切換到 GitHub Repo 模式測試表單\r\n  await modeRepo.click();\r\n  await page.waitForTimeout(500);\r\n\r\n  const repoUrlInput = page.locator('#repoUrl');\r\n  const branchInput = page.locator('#branch');\r\n  \r\n  const repoUrlVisible = await repoUrlInput.isVisible();\r\n  const branchVisible = await branchInput.isVisible();\r\n  logStep('GitHub Repo 模式表單元素', repoUrlVisible && branchVisible, { \r\n    behavior: 'Repo URL 和 Branch 輸入欄位都可見' \r\n  });\r\n\r\n  // 測試空白 Repo URL 驗證\r\n  await startButton.click();\r\n  await page.waitForTimeout(1000);\r\n  const repoErrorVisible = await errorMsg.isVisible();\r\n  let repoErrorText = '';\r\n  if (repoErrorVisible) {\r\n    repoErrorText = await errorMsg.textContent();\r\n  }\r\n  logStep('空白 Repo URL 驗證', repoErrorVisible, { \r\n    behavior: `錯誤訊息: \"${repoErrorText}\"` \r\n  });\r\n\r\n  await helpers.takeScreenshot(page, '12-repo-mode-validation');\r\n\r\n  // ===== 步驟 10: 響應式設計測試 =====\r\n  // 模擬手機尺寸\r\n  await page.setViewportSize({ width: 375, height: 667 });\r\n  await page.waitForTimeout(1000);\r\n  \r\n  // 切回 Markdown 模式以測試響應式\r\n  await modeMarkdown.click();\r\n  await page.waitForTimeout(500);\r\n\r\n  await helpers.takeScreenshot(page, '13-mobile-viewport');\r\n  \r\n  const h1MobileVisible = await h1.isVisible();\r\n  const startBtnMobileVisible = await startButton.isVisible();\r\n  logStep('手機版面配置正確', h1MobileVisible && startBtnMobileVisible, { \r\n    behavior: '375x667 視窗下主要元素可見' \r\n  });\r\n\r\n  // 模擬平板尺寸\r\n  await page.setViewportSize({ width: 768, height: 1024 });\r\n  await page.waitForTimeout(1000);\r\n  await helpers.takeScreenshot(page, '14-tablet-viewport');\r\n\r\n  logStep('平板版面配置正確', true, { \r\n    behavior: '768x1024 視窗下版面正確' \r\n  });\r\n\r\n  // 恢復桌面尺寸\r\n  await page.setViewportSize({ width: 1280, height: 720 });\r\n  await page.waitForTimeout(500);\r\n\r\n  // ===== 步驟 11: Privacy 頁面測試 =====\r\n  await page.goto(TARGET_URL + '/Privacy', { waitUntil: 'domcontentloaded', timeout: 10000 });\r\n  await page.waitForTimeout(1000);\r\n  \r\n  const privacyTitle = await page.title();\r\n  logStep('Privacy 頁面載入', true, { \r\n    behavior: `頁面標題: \"${privacyTitle}\"` \r\n  });\r\n  await helpers.takeScreenshot(page, '15-privacy-page');\r\n\r\n  // ===== 步驟 12: 導航列檢查 =====\r\n  const navLinks = page.locator('nav a, .navbar a');\r\n  const navCount = await navLinks.count();\r\n  logStep('導航列連結', navCount > 0, { \r\n    behavior: `找到 ${navCount} 個導航連結` \r\n  });\r\n\r\n  // 檢查首頁連結\r\n  const homeLink = page.locator('a[href=\"/\"]').first();\r\n  if (await homeLink.count() > 0) {\r\n    await homeLink.click();\r\n    await page.waitForTimeout(1000);\r\n    const backToHome = page.url().endsWith('/') || page.url() === TARGET_URL;\r\n    logStep('導航回首頁', backToHome, { \r\n      behavior: `當前 URL: ${page.url()}` \r\n    });\r\n    await helpers.takeScreenshot(page, '16-back-to-home');\r\n  }\r\n\r\n  // ===== 最終截圖 =====\r\n  await helpers.takeScreenshot(page, '17-final-state');\r\n  logStep('測試流程完成', true, { behavior: '所有測試步驟已執行完畢' });\r\n\r\n} catch (err) {\r\n  logStep('測試過程發生未預期錯誤', false, { \r\n    behavior: err.message, \r\n    reason: err.stack \r\n  });\r\n  await helpers.takeScreenshot(page, 'error-unexpected');\r\n} finally {\r\n  await browser.close();\r\n}\r\n";

  try {
    // 監聽 response 以取得最後的狀態碼
    const setupPageContext = (page) => {
      page.on('response', response => {
        if (response.url() === page.url()) {
          pageContext.statusCode = response.status();
        }
      });
    };

    const execute = async () => {
      // 在 code 執行前注入 context 追蹤
      try {
        ﻿// Playwright 測試腳本：Markdown Link Check 網站全面檢測
// 目標：http://localhost:5184

const TARGET_URL = 'http://localhost:5184';

initTestPlan({
  purpose: '全面檢測 Markdown Link Check 網站的 UI 與核心功能',
  workflow: '1. 首頁載入 -> 2. UI 元素檢查 -> 3. 模式切換 -> 4. 輸入驗證 -> 5. Markdown 檢測流程 -> 6. 報告驗證 -> 7. RWD 響應式測試',
  behaviors: '驗證首頁正確載入、模式切換正常、表單驗證生效、Markdown 連結檢測流程完整運作、檢測報告正確顯示、響應式設計適配'
});

const browser = await helpers.launchBrowser();
const context = await helpers.createContext(browser);
const page = await helpers.createPage(context);

try {
  // ===== 步驟 1: 首頁載入 =====
  await page.goto(TARGET_URL, { waitUntil: 'domcontentloaded', timeout: 15000 });
  await page.waitForTimeout(2000);
  
  const title = await page.title();
  logStep('首頁載入成功', true, { behavior: `頁面標題: ${title}` });
  await helpers.takeScreenshot(page, '01-homepage-loaded');

  // ===== 步驟 2: 驗證主要 UI 元素存在 =====
  const h1 = page.locator('h1');
  const h1Text = await h1.textContent();
  logStep('主標題顯示正確', h1Text.includes('Markdown Link Check'), { 
    behavior: `標題內容: "${h1Text}"` 
  });

  // 檢查模式選擇區域
  const modeMarkdown = page.locator('#modeMarkdown');
  const modeRepo = page.locator('#modeRepo');
  const isMarkdownChecked = await modeMarkdown.isChecked();
  logStep('預設模式為 Markdown 原始碼', isMarkdownChecked, { 
    behavior: 'Markdown 原始碼 radio 預設被選取' 
  });

  // 檢查 textarea 存在
  const textarea = page.locator('#markdownContent');
  const textareaVisible = await textarea.isVisible();
  logStep('Markdown 輸入區顯示', textareaVisible, { 
    behavior: 'textarea 可見' 
  });

  // 檢查開始按鈕存在
  const startButton = page.locator('#startCheck');
  const startButtonVisible = await startButton.isVisible();
  logStep('「開始檢測」按鈕顯示', startButtonVisible, { 
    behavior: '按鈕可見且可互動' 
  });

  await helpers.takeScreenshot(page, '02-ui-elements-verified');

  // ===== 步驟 3: 模式切換測試 =====
  // 切換到 GitHub Repo 模式
  await modeRepo.click();
  await page.waitForTimeout(500);

  const repoSection = page.locator('#repoUrlSection');
  const repoVisible = await repoSection.isVisible();
  logStep('切換到 GitHub Repo 模式', repoVisible, { 
    behavior: 'Repo URL 輸入區塊顯示' 
  });

  const markdownSection = page.locator('#markdownSourceSection');
  const markdownHidden = !(await markdownSection.isVisible());
  logStep('Markdown 輸入區隱藏', markdownHidden, { 
    behavior: 'Markdown 模式的 textarea 被隱藏' 
  });

  await helpers.takeScreenshot(page, '03-repo-mode');

  // 切回 Markdown 模式
  await modeMarkdown.click();
  await page.waitForTimeout(500);

  const markdownVisibleAgain = await markdownSection.isVisible();
  logStep('切回 Markdown 模式', markdownVisibleAgain, { 
    behavior: 'Markdown textarea 重新顯示' 
  });

  await helpers.takeScreenshot(page, '04-back-to-markdown-mode');

  // ===== 步驟 4: 空白內容輸入驗證 =====
  // 不填入任何內容，直接點擊開始
  await startButton.click();
  await page.waitForTimeout(1000);

  const errorMsg = page.locator('#errorMessage');
  const errorVisible = await errorMsg.isVisible();
  let errorText = '';
  if (errorVisible) {
    errorText = await errorMsg.textContent();
  }
  logStep('空白內容驗證', errorVisible && errorText.includes('請輸入'), { 
    behavior: `錯誤訊息: "${errorText}"` 
  });

  await helpers.takeScreenshot(page, '05-empty-validation');

  // ===== 步驟 5: 字元計數器測試 =====
  const testText = '# 測試標題\n這是一段測試文字';
  await textarea.fill(testText);
  await page.waitForTimeout(500);
  
  const charCountEl = page.locator('#charCount');
  const charCountText = await charCountEl.textContent();
  const expectedCount = testText.length;
  logStep('字元計數器正常', charCountText === String(expectedCount), { 
    behavior: `輸入 ${expectedCount} 字元，顯示 ${charCountText}` 
  });

  await helpers.takeScreenshot(page, '06-char-counter');

  // ===== 步驟 6: 執行 Markdown 連結檢測（核心功能）=====
  // 監聽瀏覽器 console 錯誤
  const browserErrors = [];
  page.on('console', msg => {
    if (msg.type() === 'error') {
      browserErrors.push(msg.text());
      console.log(`  [Browser Error] ${msg.text()}`);
    }
  });

  // 監聽網路回應
  page.on('response', response => {
    if (response.url().includes('/api/check/sse')) {
      console.log(`  [Network] SSE Response status: ${response.status()}`);
    }
  });

  // 準備含有連結的 Markdown 內容
  const markdownWithLinks = `# Link Check Test

This is a test document with various links:

- [Google](https://www.google.com)
- [GitHub](https://github.com)
- [Broken Link](https://this-domain-definitely-does-not-exist-xyz123.com)
- [Anchor link](#link-check-test)
`;

  await textarea.fill(markdownWithLinks);
  await page.waitForTimeout(500);
  
  logStep('填入含連結的 Markdown 內容', true, { 
    behavior: '含 3 個外部連結和 1 個錨點連結' 
  });

  await helpers.takeScreenshot(page, '07-markdown-with-links');

  // 點擊開始檢測
  await startButton.click();
  logStep('點擊「開始檢測」', true, { behavior: '觸發 SSE 連結檢測流程' });

  // 等待進度區出現
  const progressSection = page.locator('#progressSection');
  try {
    await progressSection.waitFor({ state: 'visible', timeout: 10000 });
    logStep('進度列顯示', true, { behavior: '檢測進度區塊出現' });
    await helpers.takeScreenshot(page, '08-progress-showing');
  } catch (e) {
    logStep('進度列顯示', false, { behavior: '進度區塊未出現', reason: e.message });
    await helpers.takeScreenshot(page, '08-progress-error');
  }

  // 等待取消按鈕出現
  const cancelButton = page.locator('#cancelCheck');
  const cancelVisible = await cancelButton.isVisible();
  logStep('取消按鈕顯示', cancelVisible, { 
    behavior: '檢測期間取消按鈕可見' 
  });

  // 等待檢測完成 - 使用 waitForFunction 監控 DOM 變化
  const reportSummary = page.locator('#reportSummary');
  try {
    // 方法 1: 等待 fileResults 出現內容或 reportSummary 顯示
    await page.waitForFunction(() => {
      const reportEl = document.getElementById('reportSummary');
      const fileResults = document.getElementById('fileResults');
      return (reportEl && reportEl.style.display === 'block') || 
             (fileResults && fileResults.children.length > 0);
    }, { timeout: 30000 });

    // 等 DOM 完全更新
    await page.waitForTimeout(2000);
    
    const reportVisible = await reportSummary.isVisible();
    const fileCardsNow = await page.locator('#fileResults .card').count();
    logStep('檢測完成', reportVisible || fileCardsNow > 0, { 
      behavior: `報告摘要可見: ${reportVisible}, 檔案卡片數: ${fileCardsNow}` 
    });
    await helpers.takeScreenshot(page, '09-report-summary');
  } catch (e) {
    // 額外除錯：讀取頁面內部狀態
    const debugInfo = await page.evaluate(() => {
      const reportEl = document.getElementById('reportSummary');
      const fileResults = document.getElementById('fileResults');
      const errorMsg = document.getElementById('errorMessage');
      const progressText = document.getElementById('progressText');
      return {
        reportDisplay: reportEl?.style?.display,
        fileResultsHTML: fileResults?.innerHTML?.substring(0, 500),
        errorDisplay: errorMsg?.style?.display,
        errorText: errorMsg?.textContent,
        progressText: progressText?.textContent
      };
    });
    console.log('  [Debug] 頁面狀態:', JSON.stringify(debugInfo, null, 2));
    console.log('  [Debug] 瀏覽器錯誤:', browserErrors);
    
    logStep('檢測完成，報告摘要顯示', false, { 
      behavior: `報告摘要未出現 (display: ${debugInfo.reportDisplay})`, 
      reason: `進度: ${debugInfo.progressText}, 錯誤: ${debugInfo.errorText || '無'}` 
    });
    await helpers.takeScreenshot(page, '09-report-timeout');
  }

  // ===== 步驟 7: 驗證報告內容 =====
  // 檢查檔案結果是否有出現
  const fileResults = page.locator('#fileResults .card');
  const fileResultCount = await fileResults.count();
  logStep('檔案結果卡片顯示', fileResultCount > 0, { 
    behavior: `共 ${fileResultCount} 個檔案結果卡片` 
  });

  // 檢查統計數據
  const totalLinks = page.locator('#summaryTotal');
  const totalText = await totalLinks.textContent();
  logStep('連結總數統計', parseInt(totalText) > 0, { 
    behavior: `檢測到 ${totalText} 個連結` 
  });

  const healthyCount = page.locator('#summaryHealthy');
  const healthyText = await healthyCount.textContent();
  logStep('正常連結數量統計', true, { 
    behavior: `正常連結: ${healthyText}` 
  });

  const brokenCount = page.locator('#summaryBroken');
  const brokenText = await brokenCount.textContent();
  logStep('失效連結數量統計', true, { 
    behavior: `失效連結: ${brokenText}` 
  });

  // 檢查「複製為 Markdown」按鈕
  const copyButton = page.locator('#copyMarkdown');
  const copyBtnVisible = await copyButton.isVisible();
  logStep('「複製為 Markdown」按鈕顯示', copyBtnVisible, { 
    behavior: '報告中可見複製按鈕' 
  });

  await helpers.takeScreenshot(page, '10-report-details');

  // ===== 步驟 8: 驗證連結結果表格 =====
  const resultRows = page.locator('#fileResults table tbody tr');
  const rowCount = await resultRows.count();
  logStep('連結結果列表', rowCount > 0, { 
    behavior: `表格中共 ${rowCount} 列結果` 
  });

  // 檢查狀態圖標（✅ 或 ❌）
  const statusIcons = page.locator('#fileResults table tbody tr td:first-child');
  let hasStatusIcons = false;
  if (await statusIcons.count() > 0) {
    const firstIcon = await statusIcons.first().textContent();
    hasStatusIcons = firstIcon.includes('✅') || firstIcon.includes('❌') || firstIcon.includes('⚠️') || firstIcon.includes('⏭️');
  }
  logStep('連結狀態圖標顯示', hasStatusIcons, { 
    behavior: '每列有狀態圖標（✅/❌/⚠️/⏭️）' 
  });

  await helpers.takeScreenshot(page, '11-link-results-table');

  // ===== 步驟 9: GitHub Repo 模式驗證 =====
  // 切換到 GitHub Repo 模式測試表單
  await modeRepo.click();
  await page.waitForTimeout(500);

  const repoUrlInput = page.locator('#repoUrl');
  const branchInput = page.locator('#branch');
  
  const repoUrlVisible = await repoUrlInput.isVisible();
  const branchVisible = await branchInput.isVisible();
  logStep('GitHub Repo 模式表單元素', repoUrlVisible && branchVisible, { 
    behavior: 'Repo URL 和 Branch 輸入欄位都可見' 
  });

  // 測試空白 Repo URL 驗證
  await startButton.click();
  await page.waitForTimeout(1000);
  const repoErrorVisible = await errorMsg.isVisible();
  let repoErrorText = '';
  if (repoErrorVisible) {
    repoErrorText = await errorMsg.textContent();
  }
  logStep('空白 Repo URL 驗證', repoErrorVisible, { 
    behavior: `錯誤訊息: "${repoErrorText}"` 
  });

  await helpers.takeScreenshot(page, '12-repo-mode-validation');

  // ===== 步驟 10: 響應式設計測試 =====
  // 模擬手機尺寸
  await page.setViewportSize({ width: 375, height: 667 });
  await page.waitForTimeout(1000);
  
  // 切回 Markdown 模式以測試響應式
  await modeMarkdown.click();
  await page.waitForTimeout(500);

  await helpers.takeScreenshot(page, '13-mobile-viewport');
  
  const h1MobileVisible = await h1.isVisible();
  const startBtnMobileVisible = await startButton.isVisible();
  logStep('手機版面配置正確', h1MobileVisible && startBtnMobileVisible, { 
    behavior: '375x667 視窗下主要元素可見' 
  });

  // 模擬平板尺寸
  await page.setViewportSize({ width: 768, height: 1024 });
  await page.waitForTimeout(1000);
  await helpers.takeScreenshot(page, '14-tablet-viewport');

  logStep('平板版面配置正確', true, { 
    behavior: '768x1024 視窗下版面正確' 
  });

  // 恢復桌面尺寸
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.waitForTimeout(500);

  // ===== 步驟 11: Privacy 頁面測試 =====
  await page.goto(TARGET_URL + '/Privacy', { waitUntil: 'domcontentloaded', timeout: 10000 });
  await page.waitForTimeout(1000);
  
  const privacyTitle = await page.title();
  logStep('Privacy 頁面載入', true, { 
    behavior: `頁面標題: "${privacyTitle}"` 
  });
  await helpers.takeScreenshot(page, '15-privacy-page');

  // ===== 步驟 12: 導航列檢查 =====
  const navLinks = page.locator('nav a, .navbar a');
  const navCount = await navLinks.count();
  logStep('導航列連結', navCount > 0, { 
    behavior: `找到 ${navCount} 個導航連結` 
  });

  // 檢查首頁連結
  const homeLink = page.locator('a[href="/"]').first();
  if (await homeLink.count() > 0) {
    await homeLink.click();
    await page.waitForTimeout(1000);
    const backToHome = page.url().endsWith('/') || page.url() === TARGET_URL;
    logStep('導航回首頁', backToHome, { 
      behavior: `當前 URL: ${page.url()}` 
    });
    await helpers.takeScreenshot(page, '16-back-to-home');
  }

  // ===== 最終截圖 =====
  await helpers.takeScreenshot(page, '17-final-state');
  logStep('測試流程完成', true, { behavior: '所有測試步驟已執行完畢' });

} catch (err) {
  logStep('測試過程發生未預期錯誤', false, { 
    behavior: err.message, 
    reason: err.stack 
  });
  await helpers.takeScreenshot(page, 'error-unexpected');
} finally {
  await browser.close();
}

      } catch (e) {
        throw e;
      }
    };
    await execute();
    
    // 在執行結束後，直接讀取環境變數，確保 report 使用的是腳本中 init 的最新值
    if (process.env.PW_TEST_PLAN) {
      try {
        testPlan = JSON.parse(process.env.PW_TEST_PLAN);
      } catch (e) {}
    }
  } catch (err) {
    status = '失敗';
    
    // 取得最後一個日誌步驟
    const lastStep = (global.executionLogs || []).filter(log => log.includes('[步驟') || log.includes('🚀')).pop() || '程式執行初期';
    
    // 分析錯誤類型與責任歸屬
    let errorType = '邏輯或執行錯誤';
    let suggestion = '檢查程式碼邏輯是否正確。';
    let attribution = '測試腳本錯誤 (Script Error)';
    let sourceCode = '無法取得源碼資訊';

    // 嘗試從 stack trace 擷取行號與原始碼片段
    if (err.stack) {
      const tempFileMatch = err.stack.match(/.temp-execution-[d]+.js:(d+):(d+)/);
      if (tempFileMatch) {
        const lineNum = parseInt(tempFileMatch[1]);
        const tempFilePath = path.join('C:/GitHubFolder/MarkDownLinkCheck/.github/skills/playwright-skill-v2', err.stack.match(/.temp-execution-[d]+.js/)[0]);
        if (fs.existsSync(tempFilePath)) {
          const content = fs.readFileSync(tempFilePath, 'utf8').split('\n');
          const start = Math.max(0, lineNum - 3);
          const end = Math.min(content.length, lineNum + 2);
          sourceCode = content.slice(start, end).map((line, idx) => {
            const currentLine = start + idx + 1;
            return currentLine + ': ' + line + (currentLine === lineNum ? ' <--- 錯誤發生在此處' : '');
          }).join('\n');
        }
      }
    }
    
    const msg = err.message.toLowerCase();
    
    if (msg.includes('timeout')) {
      errorType = '逾時錯誤 (Timeout)';
      if (msg.includes('navigation') || pageContext.statusCode >= 500) {
        attribution = '網站伺服器異常 (Site Server Error)';
        suggestion = '網站回應過慢或伺服器崩潰。請檢查網站後端狀態。';
      } else if (msg.includes('waiting for selector') || msg.includes('waiting for locator')) {
        attribution = '網站內容未如期出現 (Site Content Missing)';
        suggestion = '腳本在等元件，但網站沒把它生出來。可能是功能壞了，或 UI 流程變了。';
      } else {
        attribution = '測試腳本等待邏輯不足 (Script Wait Logic Error)';
        suggestion = '建議優化等待邏輯，或增加 timeout 容錯時間。';
      }
    } else if (msg.includes('selector') || msg.includes('locator')) {
      errorType = '選擇器失效 (Selector Error)';
      attribution = '網站 UI 變更 (Site UI Changed)';
      suggestion = '網站可能改版了，導致原本的 ID 或 Class 消失。請重新檢查頁面結構。';
    } else if (msg.includes('is not a function')) {
      errorType = '腳本語法錯誤 (Script Syntax Error)';
      attribution = '開發者撰寫錯誤 (Script Code Error)';
      suggestion = '腳本呼叫了不存在的 API。這 100% 是測試代碼的問題，請修正程式碼。';
    } else if (msg.includes('detached') || msg.includes('visibility')) {
      errorType = '競爭條件 (Race Condition)';
      attribution = '網站前端行為不穩定 (Site Flaky UI)';
      suggestion = '網站元件閃現或被遮擋。建議增加頁面穩定性的檢查點。';
    } else if (pageContext.statusCode >= 400) {
       errorType = 'HTTP ' + pageContext.statusCode + ' 錯誤';
       attribution = '網站環境/權限問題 (Site Environment Error)';
       suggestion = '網站本身回傳錯誤。請確認網址正確且權限正常。';
     }
    
    errorAnalysis = {
      lastStep: lastStep,
      type: errorType,
      attribution: attribution,
      message: err.message,
      suggestion: suggestion,
      sourceCode: sourceCode,
      context: pageContext
    };

    console.error('\n❌ 自動化錯誤：' + err.message);
    if (err.stack) console.error(err.stack);
  } finally {
    const duration = ((Date.now() - startTime) / 1000).toFixed(2) + ' 秒';
    
    // 從環境變數中取得 AI 總結說明（如果有）
    const aiInsight = process.env.PW_AI_INSIGHT || '';
    
    // 確保讀取到腳本中 initTestPlan 可能修改過的內容
    let finalTestPlan = helpers.currentTestPlan;
    
    // 如果環境變數中有更完整的計畫（通常由腳本內的 initTestPlan 寫入），則優先使用
    if (process.env.PW_TEST_PLAN) {
      try {
        const envPlan = JSON.parse(process.env.PW_TEST_PLAN);
        if (envPlan && envPlan.purpose !== '未定義') {
          finalTestPlan = envPlan;
        }
      } catch (e) {
        // 解析失敗則維持原樣
      }
    }
    
    await helpers.generateHtmlReport({
      logs: global.executionLogs || [],
      status: status,
      duration: duration,
      aiInsight: aiInsight,
      errorAnalysis: errorAnalysis,
      testPlan: finalTestPlan,
      stats: stats,
      testCode: rawTestCode
    });
  }
};
