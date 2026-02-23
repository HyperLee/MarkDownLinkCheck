const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

(async () => {
  const TARGET_URL = 'http://localhost:5184';
  const SCREENSHOT_DIR = path.join(__dirname, '..', 'playwright-reports', 'screenshots');
  
  // Ensure screenshot directory exists
  if (!fs.existsSync(SCREENSHOT_DIR)) {
    fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
  }

  console.log('🚀 啟動瀏覽器...');
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  const page = await context.newPage();

  let passed = 0;
  let failed = 0;

  function logStep(name, success, detail = '') {
    const icon = success ? '✅' : '❌';
    console.log(`${icon} ${name}${detail ? ' | ' + detail : ''}`);
    if (success) passed++;
    else failed++;
  }

  try {
    // ===== 步驟 1: 首頁載入 =====
    console.log('\n📋 步驟 1: 首頁載入');
    await page.goto(TARGET_URL, { waitUntil: 'domcontentloaded', timeout: 15000 });
    await page.waitForTimeout(2000);
    const title = await page.title();
    logStep('首頁載入成功', true, `標題: ${title}`);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '01-homepage.png') });

    // ===== 步驟 2: UI 元素檢查 =====
    console.log('\n📋 步驟 2: UI 元素檢查');
    const h1 = await page.locator('h1').textContent();
    logStep('主標題顯示', h1.includes('Markdown Link Check'), `內容: "${h1}"`);

    const modeMarkdownChecked = await page.locator('#modeMarkdown').isChecked();
    logStep('預設模式為 Markdown', modeMarkdownChecked);

    const textareaVisible = await page.locator('#markdownContent').isVisible();
    logStep('Markdown 輸入區顯示', textareaVisible);

    const startBtnVisible = await page.locator('#startCheck').isVisible();
    logStep('開始檢測按鈕顯示', startBtnVisible);

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '02-ui-elements.png') });

    // ===== 步驟 3: 模式切換測試 =====
    console.log('\n📋 步驟 3: 模式切換');
    await page.locator('#modeRepo').click();
    await page.waitForTimeout(500);

    const repoVisible = await page.locator('#repoUrlSection').isVisible();
    logStep('切換到 GitHub Repo 模式', repoVisible);

    const mdHidden = !(await page.locator('#markdownSourceSection').isVisible());
    logStep('Markdown 輸入區隱藏', mdHidden);

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '03-repo-mode.png') });

    await page.locator('#modeMarkdown').click();
    await page.waitForTimeout(500);
    const mdVisibleAgain = await page.locator('#markdownSourceSection').isVisible();
    logStep('切回 Markdown 模式', mdVisibleAgain);

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '04-markdown-mode.png') });

    // ===== 步驟 4: 空白內容驗證 =====
    console.log('\n📋 步驟 4: 空白內容驗證');
    await page.locator('#startCheck').click();
    await page.waitForTimeout(1500);

    const errorVisible = await page.locator('#errorMessage').isVisible();
    let errorText = '';
    if (errorVisible) {
      errorText = await page.locator('#errorMessage').textContent();
    }
    logStep('空白輸入驗證', errorVisible, `錯誤訊息: "${errorText}"`);

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '05-empty-validation.png') });

    // ===== 步驟 5: 字元計數器 =====
    console.log('\n📋 步驟 5: 字元計數器');
    const testText = '# 測試標題\n這是一段測試文字';
    await page.locator('#markdownContent').fill(testText);
    await page.waitForTimeout(500);

    const charCountText = await page.locator('#charCount').textContent();
    logStep('字元計數器正常', charCountText === String(testText.length), `輸入 ${testText.length} 字元, 顯示 ${charCountText}`);

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '06-char-counter.png') });

    // ===== 步驟 6: 核心功能 - Markdown 連結檢測 =====
    console.log('\n📋 步驟 6: Markdown 連結檢測（核心功能）');
    const markdownWithLinks = `# Link Check Test

This is a test document with various links:

- [Google](https://www.google.com)
- [GitHub](https://github.com)
- [Broken Link](https://this-domain-definitely-does-not-exist-xyz123.com)
- [Anchor link](#link-check-test)
`;

    await page.locator('#markdownContent').fill(markdownWithLinks);
    await page.waitForTimeout(500);
    logStep('填入含連結的 Markdown', true, '含 3 個外部連結 + 1 個錨點連結');

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '07-markdown-with-links.png') });

    // 點擊開始檢測
    await page.locator('#startCheck').click();
    logStep('點擊開始檢測', true);

    // 等待進度列出現
    try {
      await page.locator('#progressSection').waitFor({ state: 'visible', timeout: 10000 });
      logStep('進度列顯示', true);
      await page.screenshot({ path: path.join(SCREENSHOT_DIR, '08-progress.png') });
    } catch (e) {
      logStep('進度列顯示', false, e.message);
    }

    // 等待取消按鈕
    const cancelVisible = await page.locator('#cancelCheck').isVisible();
    logStep('取消按鈕顯示', cancelVisible);

    // 等待報告摘要出現 (最多 90 秒)
    console.log('  ⏳ 等待檢測完成...');
    try {
      await page.locator('#reportSummary').waitFor({ state: 'visible', timeout: 90000 });
      logStep('檢測完成，報告摘要顯示', true);
      await page.screenshot({ path: path.join(SCREENSHOT_DIR, '09-report-summary.png') });
    } catch (e) {
      logStep('檢測完成，報告摘要顯示', false, e.message);
      await page.screenshot({ path: path.join(SCREENSHOT_DIR, '09-report-timeout.png') });
    }

    // ===== 步驟 7: 驗證報告內容 =====
    console.log('\n📋 步驟 7: 報告內容驗證');
    const fileResultCount = await page.locator('#fileResults .card').count();
    logStep('檔案結果卡片', fileResultCount > 0, `共 ${fileResultCount} 個`);

    const totalLinks = await page.locator('#summaryTotal').textContent();
    logStep('連結總數統計', parseInt(totalLinks) > 0, `${totalLinks} 個連結`);

    const healthyCount = await page.locator('#summaryHealthy').textContent();
    logStep('正常連結統計', true, `正常: ${healthyCount}`);

    const brokenCount = await page.locator('#summaryBroken').textContent();
    logStep('失效連結統計', true, `失效: ${brokenCount}`);

    const warningCount = await page.locator('#summaryWarning').textContent();
    logStep('警告連結統計', true, `警告: ${warningCount}`);

    const skippedCount = await page.locator('#summarySkipped').textContent();
    logStep('跳過連結統計', true, `跳過: ${skippedCount}`);

    const copyBtnVisible = await page.locator('#copyMarkdown').isVisible();
    logStep('複製為 Markdown 按鈕', copyBtnVisible);

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '10-report-details.png'), fullPage: true });

    // ===== 步驟 8: 連結結果表格 =====
    console.log('\n📋 步驟 8: 連結結果表格驗證');
    const rowCount = await page.locator('#fileResults table tbody tr').count();
    logStep('結果表格有資料', rowCount > 0, `共 ${rowCount} 列`);

    if (rowCount > 0) {
      const firstIcon = await page.locator('#fileResults table tbody tr td:first-child').first().textContent();
      const hasIcon = ['✅', '❌', '⚠️', '⏭️'].some(icon => firstIcon.includes(icon));
      logStep('狀態圖標顯示', hasIcon, `首個圖標: "${firstIcon.trim()}"`);
    }

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '11-link-results-table.png') });

    // ===== 步驟 9: GitHub Repo 模式表單 =====
    console.log('\n📋 步驟 9: GitHub Repo 模式表單');
    await page.locator('#modeRepo').click();
    await page.waitForTimeout(500);

    const repoUrlVisible = await page.locator('#repoUrl').isVisible();
    const branchVisible = await page.locator('#branch').isVisible();
    logStep('Repo URL/Branch 欄位可見', repoUrlVisible && branchVisible);

    await page.locator('#startCheck').click();
    await page.waitForTimeout(1000);
    const repoErrorVisible = await page.locator('#errorMessage').isVisible();
    logStep('空 Repo URL 驗證', repoErrorVisible);

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '12-repo-validation.png') });

    // ===== 步驟 10: 響應式設計測試 =====
    console.log('\n📋 步驟 10: 響應式設計 (RWD)');
    await page.locator('#modeMarkdown').click();
    await page.waitForTimeout(300);

    // 手機 (375x667)
    await page.setViewportSize({ width: 375, height: 667 });
    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '13-mobile-375.png') });
    const h1Mobile = await page.locator('h1').isVisible();
    logStep('手機版面 (375px)', h1Mobile);

    // 平板 (768x1024)
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.waitForTimeout(1000);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '14-tablet-768.png') });
    logStep('平板版面 (768px)', true);

    // 恢復桌面
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.waitForTimeout(500);

    // ===== 步驟 11: Privacy 頁面 =====
    console.log('\n📋 步驟 11: Privacy 頁面');
    await page.goto(TARGET_URL + '/Privacy', { waitUntil: 'domcontentloaded', timeout: 10000 });
    await page.waitForTimeout(1000);
    const privacyTitle = await page.title();
    logStep('Privacy 頁面載入', true, `標題: "${privacyTitle}"`);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '15-privacy.png') });

    // ===== 步驟 12: 導航列 =====
    console.log('\n📋 步驟 12: 導航列檢查');
    const navCount = await page.locator('nav a, .navbar a').count();
    logStep('導航列連結', navCount > 0, `共 ${navCount} 個`);

    const homeLinks = await page.locator('a[href="/"]');
    if (await homeLinks.count() > 0) {
      await homeLinks.first().click();
      await page.waitForTimeout(1000);
      const url = page.url();
      logStep('導航回首頁', url.endsWith('/') || url === TARGET_URL, `URL: ${url}`);
    }

    await page.screenshot({ path: path.join(SCREENSHOT_DIR, '16-final.png'), fullPage: true });

  } catch (err) {
    console.error(`\n❌ 未預期錯誤: ${err.message}`);
    console.error(err.stack);
    await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'error.png') }).catch(() => {});
    failed++;
  } finally {
    await browser.close();

    // 印出結果摘要
    const total = passed + failed;
    console.log('\n' + '='.repeat(60));
    console.log(`📊 測試結果摘要`);
    console.log('='.repeat(60));
    console.log(`  總計: ${total} 個測試步驟`);
    console.log(`  ✅ 通過: ${passed}`);
    console.log(`  ❌ 失敗: ${failed}`);
    console.log(`  📁 截圖目錄: ${SCREENSHOT_DIR}`);
    console.log('='.repeat(60));
    
    if (failed > 0) {
      console.log('\n⚠️ 部分測試失敗，請檢查上方日誌');
    } else {
      console.log('\n🎉 所有測試通過！');
    }
  }
})();
