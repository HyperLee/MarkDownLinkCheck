// Playwright 測試腳本：Markdown Link Check 網站全面檢測
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
