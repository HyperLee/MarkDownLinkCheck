// playwright-helpers.js
// Playwright 自動化重複使用的工具函式

const { chromium, firefox, webkit } = require('playwright');
const fs = require('fs');
const path = require('path');

// 測試狀態管理
let stats = { passed: 0, failed: 0, total: 0, steps: [] };
let currentTestPlan = {
  purpose: '未定義',
  workflow: '未定義',
  behaviors: '未定義'
};

/**
 * 允許外部設定共享的 stats 物件
 * @param {Object} sharedStats 
 */
function setGlobalStats(sharedStats) {
  stats = sharedStats;
}

/**
 * 初始化測試計畫資訊
 * @param {Object} plan - 包含 purpose, workflow, behaviors 的物件
 */
function initTestPlan(plan = {}) {
  // 更新現有物件的屬性，而不是重新賦值變數，確保匯出的引用保持同步
  currentTestPlan.purpose = plan.purpose || '未定義';
  currentTestPlan.workflow = plan.workflow || '未定義';
  currentTestPlan.behaviors = plan.behaviors || '未定義';

  // 同時存入環境變數以供 run.js 或其他工具讀取
  process.env.PW_TEST_PLAN = JSON.stringify(currentTestPlan);

  console.log('📋 測試計畫已初始化:');
  console.log(`   🎯 目的: ${currentTestPlan.purpose}`);
  console.log(`   🛤️ 流程: ${currentTestPlan.workflow}`);
  console.log(`   ⚙️ 行為: ${currentTestPlan.behaviors}`);
}

/**
 * 記錄測試步驟
 * @param {string} name - 步驟名稱
 * @param {boolean} success - 是否成功
 * @param {Object} options - 包含 behavior, reason 的物件
 */
function logStep(name, success = true, options = {}) {
  const icon = success ? '✅' : '❌';
  const reason = options.reason || '';
  const behavior = options.behavior || '';

  let logMsg = `${icon} [步驟] ${name}`;
  if (behavior) logMsg += ` | 行為: ${behavior}`;
  if (reason) logMsg += ` | 理由: ${reason}`;

  console.log(logMsg);

  stats.total++;
  stats.steps.push({
    name,
    success,
    behavior: behavior,
    reason: reason
  });

  if (success) {
    stats.passed++;
  } else {
    stats.failed++;
  }
}

/**
 * 從環境變數解析額外的 HTTP 標頭。
 * 支援兩種格式：
 * - PW_HEADER_NAME + PW_HEADER_VALUE：單個標頭（簡單且常見的情況）
 * - PW_EXTRA_HEADERS：多個標頭的 JSON 物件（進階）
 * 若兩者皆設定，則以單個標頭格式優先。
 * @returns {Object|null} 標頭物件，若未設定則為 null
 */
function getExtraHeadersFromEnv() {
  const headerName = process.env.PW_HEADER_NAME;
  const headerValue = process.env.PW_HEADER_VALUE;

  if (headerName && headerValue) {
    return { [headerName]: headerValue };
  }

  const headersJson = process.env.PW_EXTRA_HEADERS;
  if (headersJson) {
    try {
      const parsed = JSON.parse(headersJson);
      if (typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed)) {
        return parsed;
      }
      console.warn('PW_EXTRA_HEADERS 必須是 JSON 物件，忽略中...');
    } catch (e) {
      console.warn('無法將 PW_EXTRA_HEADERS 解析為 JSON：', e.message);
    }
  }

  return null;
}

/**
 * 使用標準配置啟動瀏覽器
 * @param {string} browserType - 'chromium', 'firefox', 或 'webkit'
 * @param {Object} options - 額外的啟動選項
 */
async function launchBrowser(browserType = 'chromium', options = {}) {
  const defaultOptions = {
    headless: process.env.HEADLESS !== 'false',
    slowMo: process.env.SLOW_MO ? parseInt(process.env.SLOW_MO) : 0,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  };

  const browsers = { chromium, firefox, webkit };
  const browser = browsers[browserType];

  if (!browser) {
    throw new Error(`無效的瀏覽器類型：${browserType}`);
  }

  return await browser.launch({ ...defaultOptions, ...options });
}

/**
 * 建立具有視口與使用者代理的新頁面
 * @param {Object} context - 瀏覽器上下文
 * @param {Object} options - 頁面選項
 */
async function createPage(context, options = {}) {
  const page = await context.newPage();

  if (options.viewport) {
    await page.setViewportSize(options.viewport);
  }

  if (options.userAgent) {
    await page.setExtraHTTPHeaders({
      'User-Agent': options.userAgent
    });
  }

  // 設定預設逾時
  page.setDefaultTimeout(options.timeout || 30000);

  // 🎭 測試憲法：自動捕捉瀏覽器端日誌
  page.on('console', msg => {
    const text = msg.text();
    const type = msg.type();
    // 過濾掉一些常見的雜訊（如廣告或追蹤腳本的錯誤）
    const noise = ['google-analytics', 'doubleclick', 'facebook.net', 'adsbygoogle'];
    if (!noise.some(n => text.includes(n))) {
      // 使用與 Terminal 一致的格式輸出，方便除錯
      console.log(`[瀏覽器 ${type.toUpperCase()}] ${text}`);
    }
  });

  return page;
}

/**
 * 智慧等待頁面就緒
 * @param {Object} page - Playwright 頁面
 * @param {Object} options - 等待選項
 */
async function waitForPageReady(page, options = {}) {
  const waitOptions = {
    waitUntil: options.waitUntil || 'networkidle',
    timeout: options.timeout || 30000
  };

  try {
    await page.waitForLoadState(waitOptions.waitUntil, {
      timeout: waitOptions.timeout
    });
  } catch (e) {
    console.warn('頁面載入逾時，繼續執行...');
  }

  // 若提供選擇器，則額外等待動態內容
  if (options.waitForSelector) {
    await page.waitForSelector(options.waitForSelector, {
      timeout: options.timeout
    });
  }
}

/**
 * 具有重試邏輯的安全點擊
 * @param {Object} page - Playwright 頁面
 * @param {string} selector - 元件選擇器
 * @param {Object} options - 點擊選項
 */
async function safeClick(page, selector, options = {}) {
  const maxRetries = options.retries || 3;
  const retryDelay = options.retryDelay || 1000;

  for (let i = 0; i < maxRetries; i++) {
    try {
      await page.waitForSelector(selector, {
        state: 'visible',
        timeout: options.timeout || 5000
      });
      await page.click(selector, {
        force: options.force || false,
        timeout: options.timeout || 5000
      });
      return true;
    } catch (e) {
      if (i === maxRetries - 1) {
        console.error(`在 ${maxRetries} 次嘗試後仍無法點擊 ${selector}`);
        throw e;
      }
      console.log(`正在重試點擊 ${selector} (${i + 1}/${maxRetries})`);
      await page.waitForTimeout(retryDelay);
    }
  }
}

/**
 * 安全的文字輸入，在輸入前先清除內容
 * @param {Object} page - Playwright 頁面
 * @param {string} selector - 輸入框選擇器
 * @param {string} text - 要輸入的文字
 * @param {Object} options - 輸入選項
 */
async function safeType(page, selector, text, options = {}) {
  await page.waitForSelector(selector, {
    state: 'visible',
    timeout: options.timeout || 10000
  });

  if (options.clear !== false) {
    await page.fill(selector, '');
  }

  if (options.slow) {
    await page.type(selector, text, { delay: options.delay || 100 });
  } else {
    await page.fill(selector, text);
  }
}

/**
 * 從多個元件提取文字
 * @param {Object} page - Playwright 頁面
 * @param {string} selector - 元件選擇器
 */
async function extractTexts(page, selector) {
  await page.waitForSelector(selector, { timeout: 10000 });
  return await page.$$eval(selector, elements =>
    elements.map(el => el.textContent?.trim()).filter(Boolean)
  );
}

/**
 * 取得台灣台北時間 (UTC+8) 的時間戳記
 * @param {string} format - 'filename' (用於檔名), 'display' (用於顯示)
 * @returns {string} 格式化後的時間字串
 */
function getTaipeiTimestamp(format = 'filename') {
  const now = new Date();
  // 加上 8 小時的毫秒數
  const taipeiTime = new Date(now.getTime() + (8 * 60 * 60 * 1000));
  const isoString = taipeiTime.toISOString(); // yyyy-mm-ddThh:mm:ss.mmmZ

  if (format === 'filename') {
    // 格式：yyyy-MM-dd_HH-mm-ss
    return isoString.replace('T', '_').replace(/:/g, '-').split('.')[0];
  } else {
    // 格式：yyyy/MM/dd HH:mm:ss
    return isoString.replace('T', ' ').replace(/-/g, '/').split('.')[0];
  }
}

/**
 * 帶有時間戳記的截圖
 * @param {Object} page - Playwright 頁面
 * @param {string} name - 截圖名稱
 * @param {Object} options - 截圖選項
 */
async function takeScreenshot(page, name, options = {}) {
  const timestamp = getTaipeiTimestamp('filename');
  const baseDir = process.env.PW_REPORT_DIR || path.join(process.cwd(), 'playwright-reports');
  const reportDir = path.join(baseDir, 'screenshots');

  if (!fs.existsSync(reportDir)) {
    fs.mkdirSync(reportDir, { recursive: true });
  }

  const filename = `${name}-${timestamp}.png`;
  const filePath = path.join(reportDir, filename);

  await page.screenshot({
    path: filePath,
    fullPage: options.fullPage !== false,
    ...options
  });

  console.log(`截圖已儲存：${filePath}`);
  return filePath;
}

/**
 * 處理身份驗證
 * @param {Object} page - Playwright 頁面
 * @param {Object} credentials - 使用者名稱與密碼
 * @param {Object} selectors - 登入表單選擇器
 */
async function authenticate(page, credentials, selectors = {}) {
  const defaultSelectors = {
    username: 'input[name="username"], input[name="email"], #username, #email',
    password: 'input[name="password"], #password',
    submit: 'button[type="submit"], input[type="submit"], button:has-text("Login"), button:has-text("Sign in"), button:has-text("登入")'
  };

  const finalSelectors = { ...defaultSelectors, ...selectors };

  await safeType(page, finalSelectors.username, credentials.username);
  await safeType(page, finalSelectors.password, credentials.password);
  await safeClick(page, finalSelectors.submit);

  // 等待導航或成功指示器
  await Promise.race([
    page.waitForNavigation({ waitUntil: 'networkidle' }),
    page.waitForSelector(selectors.successIndicator || '.dashboard, .user-menu, .logout, .user-profile', { timeout: 10000 })
  ]).catch(() => {
    console.log('登入可能已完成但未觸發導航');
  });
}

/**
 * 捲動頁面
 * @param {Object} page - Playwright 頁面
 * @param {string} direction - 'down', 'up', 'top', 'bottom'
 * @param {number} distance - 捲動像素（用於 up/down）
 */
async function scrollPage(page, direction = 'down', distance = 500) {
  switch (direction) {
    case 'down':
      await page.evaluate(d => window.scrollBy(0, d), distance);
      break;
    case 'up':
      await page.evaluate(d => window.scrollBy(0, -d), distance);
      break;
    case 'top':
      await page.evaluate(() => window.scrollTo(0, 0));
      break;
    case 'bottom':
      await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
      break;
  }
  await page.waitForTimeout(500); // 等待捲動動畫
}

/**
 * 提取表格資料
 * @param {Object} page - Playwright 頁面
 * @param {string} tableSelector - 表格選擇器
 */
async function extractTableData(page, tableSelector) {
  await page.waitForSelector(tableSelector);

  return await page.evaluate((selector) => {
    const table = document.querySelector(selector);
    if (!table) return null;

    const headers = Array.from(table.querySelectorAll('thead th')).map(th =>
      th.textContent?.trim()
    );

    const rows = Array.from(table.querySelectorAll('tbody tr')).map(tr => {
      const cells = Array.from(tr.querySelectorAll('td'));
      if (headers.length > 0) {
        return cells.reduce((obj, cell, index) => {
          obj[headers[index] || `column_${index}`] = cell.textContent?.trim();
          return obj;
        }, {});
      } else {
        return cells.map(cell => cell.textContent?.trim());
      }
    });

    return { headers, rows };
  }, tableSelector);
}

/**
 * 等待並關閉 Cookie 橫幅
 * @param {Object} page - Playwright 頁面
 * @param {number} timeout - 最大等待時間
 */
async function handleCookieBanner(page, timeout = 3000) {
  const commonSelectors = [
    'button:has-text("Accept")',
    'button:has-text("Accept all")',
    'button:has-text("OK")',
    'button:has-text("Got it")',
    'button:has-text("I agree")',
    'button:has-text("接受")',
    'button:has-text("同意")',
    '.cookie-accept',
    '#cookie-accept',
    '[data-testid="cookie-accept"]'
  ];

  for (const selector of commonSelectors) {
    try {
      const element = await page.waitForSelector(selector, {
        timeout: timeout / commonSelectors.length,
        state: 'visible'
      });
      if (element) {
        await element.click();
        console.log('Cookie 橫幅已關閉');
        return true;
      }
    } catch (e) {
      // 繼續嘗試下一個選擇器
    }
  }

  return false;
}

/**
 * 使用指數退避演算法重試函式
 * @param {Function} fn - 要重試的函式
 * @param {number} maxRetries - 最大重試次數
 * @param {number} initialDelay - 初始延遲（毫秒）
 */
async function retryWithBackoff(fn, maxRetries = 3, initialDelay = 1000) {
  let lastError;

  for (let i = 0; i < maxRetries; i++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error;
      const delay = initialDelay * Math.pow(2, i);
      console.log(`第 ${i + 1} 次嘗試失敗，將於 ${delay} 毫秒後重試...`);
      await new Promise(resolve => setTimeout(resolve, delay));
    }
  }

  throw lastError;
}

/**
 * 建立具有常用設定的瀏覽器上下文
 * @param {Object} browser - 瀏覽器執行個體
 * @param {Object} options - 上下文選項
 */
async function createContext(browser, options = {}) {
  const envHeaders = getExtraHeadersFromEnv();

  // 將環境變數中的標頭與傳入的選項合併
  const mergedHeaders = {
    ...envHeaders,
    ...options.extraHTTPHeaders
  };

  const reportDir = process.env.PW_REPORT_DIR || path.join(process.cwd(), 'playwright-report-media');

  const defaultOptions = {
    viewport: { width: 1280, height: 720 },
    userAgent: options.mobile
      ? 'Mozilla/5.0 (iPhone; CPU iPhone OS 14_7_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.2 Mobile/15E148 Safari/604.1'
      : undefined,
    permissions: options.permissions || [],
    geolocation: options.geolocation,
    locale: options.locale || 'zh-TW',
    timezoneId: options.timezoneId || 'Asia/Taipei',
    recordVideo: options.recordVideo || {
      dir: path.join(reportDir, 'videos/'),
      size: { width: 1280, height: 720 }
    },
    // 僅在有標頭時才包含 extraHTTPHeaders
    ...(Object.keys(mergedHeaders).length > 0 && { extraHTTPHeaders: mergedHeaders })
  };

  return await browser.newContext({ ...defaultOptions, ...options });
}

/**
 * 在常用連接埠上偵測執行中的開發伺服器
 * @param {Array<number>} customPorts - 額外要檢查的連接埠
 * @returns {Promise<Array>} 偵測到的伺服器 URL 陣列
 */
async function detectDevServers(customPorts = []) {
  const http = require('http');

  // 常見的開發伺服器連接埠
  const commonPorts = [3000, 3001, 3002, 5173, 8080, 8000, 4200, 5000, 9000, 1234];
  const allPorts = [...new Set([...commonPorts, ...customPorts])];

  const detectedServers = [];

  console.log('🔍 正在檢查執行中的開發伺服器...');

  for (const port of allPorts) {
    try {
      await new Promise((resolve, reject) => {
        const req = http.request({
          hostname: 'localhost',
          port: port,
          path: '/',
          method: 'HEAD',
          timeout: 500
        }, (res) => {
          if (res.statusCode < 500) {
            detectedServers.push(`http://localhost:${port}`);
            console.log(`  ✅ 在連接埠 ${port} 發現伺服器`);
          }
          resolve();
        });

        req.on('error', () => resolve());
        req.on('timeout', () => {
          req.destroy();
          resolve();
        });

        req.end();
      });
    } catch (e) {
      // 連接埠不可用，繼續執行
    }
  }

  if (detectedServers.length === 0) {
    console.log('  ❌ 未偵測到開發伺服器');
  }

  return detectedServers;
}

/**
 * 生成精美的 HTML 報告，包含圖片與影片連結
 * @param {Object} summary - 包含文字總結與日誌的物件
 */
async function generateHtmlReport(summary = {}) {
  const reportDir = process.env.PW_REPORT_DIR || path.join(process.cwd(), 'playwright-reports');
  const screenshotDir = path.join(reportDir, 'screenshots');
  const videoDir = path.join(reportDir, 'videos');

  const executionLogs = summary.logs || [];
  const status = summary.status || '未知';
  const duration = summary.duration || 'N/A';
  const aiInsight = summary.aiInsight || '';
  const errorAnalysis = summary.errorAnalysis || null;
  const reportTestPlan = summary.testPlan || currentTestPlan;
  const currentStats = summary.stats || stats || { passed: 0, failed: 0, total: 0, steps: [] };
  const testCode = summary.testCode || '';
  const reportTime = getTaipeiTimestamp('display');

  // 整理步驟資料
  const steps = currentStats.steps || [];
  const enhancedSteps = steps.map(step => {
    return step;
  });

  // 確保目錄存在
  if (!fs.existsSync(reportDir)) {
    fs.mkdirSync(reportDir, { recursive: true });
  }

  let screenshots = [];
  if (fs.existsSync(screenshotDir)) {
    screenshots = fs.readdirSync(screenshotDir)
      .filter(f => f.endsWith('.png'))
      .sort((a, b) => {
        // 嘗試從檔名提取時間戳記進行排序 (例如 2026-02-14_11-18-22)
        const timeA = a.match(/\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}/);
        const timeB = b.match(/\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}/);
        if (timeA && timeB) return timeA[0].localeCompare(timeB[0]);
        return a.localeCompare(b);
      });
  }

  let videos = [];
  if (fs.existsSync(videoDir)) {
    videos = fs.readdirSync(videoDir)
      .filter(f => f.endsWith('.webm'))
      .sort();
  }

  // 確保截圖與影片在 HTML 中能正確顯示 (使用相對路徑)
  const screenshotLinks = screenshots.map((s, idx) => `
    <div class="media-item" onclick="openLightbox(${idx})">
      <img src="screenshots/${s}" alt="${s}">
      <div class="media-caption">${s}</div>
    </div>
  `).join('');

  const videoLinks = videos.map(v => `
    <div class="media-item video-item">
      <video autoplay loop muted playsinline controls>
        <source src="videos/${v}" type="video/webm">
        您的瀏覽器不支援影片標籤。
      </video>
      <div class="media-caption">${v}</div>
    </div>
  `).join('');

  // 簡單的 Markdown 格式化處理 (將換行符號轉為 <br>, 粗體轉為 <strong>)
  const formattedAiInsight = aiInsight
    .replace(/\n/g, '<br>')
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/^- (.*)/gm, '• $1');

  const htmlContent = `
<!DOCTYPE html>
<html lang="zh-TW">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Playwright 自動化測試報告</title>
    <!-- Highlight.js 語法高亮 -->
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github-dark.min.css">
    <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/languages/javascript.min.js"></script>
    <style>
        body { font-family: "Microsoft JhengHei", sans-serif; line-height: 1.6; color: #333; max-width: 1200px; margin: 0 auto; padding: 20px; background-color: #f4f7f6; }
        h1 { color: #2c3e50; text-align: center; border-bottom: 2px solid #3498db; padding-bottom: 10px; }
        .section { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 5px rgba(0,0,0,0.1); margin-bottom: 20px; }
        h2 { color: #2980b9; border-left: 5px solid #3498db; padding-left: 10px; margin-top: 0; }
        .summary-box { display: flex; gap: 20px; margin-bottom: 20px; }
        .summary-item { flex: 1; background: #ebf5fb; padding: 15px; border-radius: 8px; text-align: center; border: 1px solid #d6eaf8; }
        .summary-label { font-size: 14px; color: #5dade2; font-weight: bold; display: block; }
        .summary-value { font-size: 18px; color: #2e86c1; font-weight: bold; }
        
        /* 測試計畫區塊 */
        .test-plan { background: #fdfcfe; border: 1px solid #dcdde1; border-left: 6px solid #6c5ce7; padding: 20px; border-radius: 8px; margin-bottom: 25px; }
        .test-plan-header { font-weight: bold; color: #6c5ce7; margin-bottom: 12px; font-size: 18px; border-bottom: 1px dashed #dcdde1; padding-bottom: 8px; }
        .test-plan-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 15px; }
        .test-plan-item { background: #fff; padding: 12px; border-radius: 6px; border: 1px solid #f0f0f0; }
        .test-plan-label { font-weight: bold; color: #4b4b4b; font-size: 14px; margin-bottom: 5px; display: block; }
        .test-plan-value { font-size: 14px; color: #2d3436; white-space: pre-wrap; }

        /* 步驟清單 */
        .steps-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
        .steps-column { background: #f9f9f9; border-radius: 8px; overflow: hidden; }
        .steps-header { padding: 12px; font-weight: bold; color: white; text-align: center; }
        .steps-header.success { background-color: #27ae60; }
        .steps-header.error { background-color: #e74c3c; }
        .step-item { padding: 12px; border-bottom: 1px solid #eee; background: white; margin: 8px; border-radius: 4px; border-left: 4px solid #ddd; }
        .step-main { font-weight: bold; display: flex; align-items: flex-start; gap: 8px; }
        .step-details { font-size: 13px; color: #666; margin-top: 5px; padding-left: 24px; }
        .step-detail-row { margin-bottom: 2px; }
        .step-detail-label { font-weight: bold; color: #888; margin-right: 5px; }
        .no-data { text-align: center; padding: 20px; color: #999; font-style: italic; }

        /* 媒體網格 */
        .media-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 15px; margin-top: 15px; }
        .media-item { background: #f8f9fa; border: 1px solid #dee2e6; border-radius: 8px; overflow: hidden; cursor: pointer; position: relative; transition: transform 0.2s, box-shadow 0.2s; }
        .media-item:hover { transform: translateY(-3px); box-shadow: 0 4px 10px rgba(0,0,0,0.15); border-color: #3498db; }
        .media-item img, .media-item video { width: 100%; height: 180px; object-fit: cover; display: block; }
        .media-caption { padding: 8px; font-size: 12px; background: #fff; border-top: 1px solid #eee; word-break: break-all; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .media-item:hover .media-caption { white-space: normal; overflow: visible; position: absolute; bottom: 0; left: 0; right: 0; background: rgba(255,255,255,0.95); z-index: 2; }
        
        /* 放大鏡圖示 */
        .media-item::after { content: "🔍"; position: absolute; top: 10px; right: 10px; background: rgba(255,255,255,0.8); width: 30px; height: 30px; display: flex; align-items: center; justify-content: center; border-radius: 50%; font-size: 16px; opacity: 0; transition: opacity 0.3s; pointer-events: none; box-shadow: 0 2px 5px rgba(0,0,0,0.2); }
        .media-item:hover::after { opacity: 1; }
        .media-item.video-item::after { content: "🎥"; }

        /* 燈箱效果 (Lightbox) */
        .lightbox { display: none; position: fixed; z-index: 9999; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.9); justify-content: center; align-items: center; overflow: hidden; }
        .lightbox-content { position: relative; max-width: 95%; max-height: 95%; display: flex; flex-direction: column; align-items: center; cursor: grab; }
        .lightbox-content:active { cursor: grabbing; }
        .lightbox-img { max-width: 100%; max-height: 85vh; object-fit: contain; box-shadow: 0 0 20px rgba(0,0,0,0.5); border: 2px solid #fff; transition: transform 0.1s ease-out; transform-origin: center; }
        .lightbox-caption { color: #fff; margin-top: 15px; font-size: 16px; background: rgba(0,0,0,0.5); padding: 5px 15px; border-radius: 20px; z-index: 10; }
        .lightbox-close { position: absolute; top: -40px; right: 0; color: #fff; font-size: 30px; cursor: pointer; font-weight: bold; transition: color 0.3s; }
        .lightbox-close:hover { color: #ff4757; }
        .lightbox-nav { position: absolute; top: 50%; transform: translateY(-50%); color: #fff; font-size: 50px; cursor: pointer; user-select: none; background: rgba(255,255,255,0.1); padding: 10px; border-radius: 5px; transition: background 0.3s; }
        .lightbox-nav:hover { background: rgba(255,255,255,0.3); }
        .lightbox-prev { left: -80px; }
        .lightbox-next { right: -80px; }
        .lightbox-counter { position: absolute; bottom: -40px; color: #aaa; font-size: 14px; }

        .log-container { background: #2d3436; color: #dfe6e9; padding: 15px; border-radius: 8px; font-family: 'Courier New', Courier, monospace; font-size: 13px; white-space: pre-wrap; margin-bottom: 25px; border: 1px solid #dcdde1; }
        .test-code-block { margin-bottom: 25px; border-radius: 8px; overflow: hidden; border: 1px solid #dcdde1; }
        .test-code-header { background: #2d3436; color: #fff; padding: 10px 15px; font-weight: bold; display: flex; justify-content: space-between; align-items: center; }
        .ai-insight { background: #fff9db; border-left: 5px solid #fcc419; padding: 15px; border-radius: 4px; margin-bottom: 20px; }
        .status-success { color: #27ae60; font-weight: bold; }
        .status-error { color: #e74c3c; font-weight: bold; }
        .timestamp { font-size: 12px; color: #7f8c8d; text-align: center; margin-top: 20px; }
        
        @media (max-width: 768px) {
            .steps-grid { grid-template-columns: 1fr; }
            .lightbox-nav { font-size: 30px; padding: 5px; }
            .lightbox-prev { left: 10px; }
            .lightbox-next { right: 10px; }
        }
    </style>
</head>
<body>
    <h1>🎭 Playwright 自動化測試報告</h1>

    <div class="section">
        <h2>📊 執行總結 (Summary)</h2>
        <div class="summary-box">
            <div class="summary-item">
                <span class="summary-label">最終狀態</span>
                <span class="summary-value ${status === '成功' ? 'status-success' : 'status-error'}">${status}</span>
            </div>
            <div class="summary-item">
                <span class="summary-label">執行耗時</span>
                <span class="summary-value">${duration}</span>
            </div>
            <div class="summary-item">
                <span class="summary-label">報告生成時間 (台北)</span>
                <span class="summary-value">${reportTime}</span>
            </div>
            <div class="summary-item">
                <span class="summary-label">多步驟統計</span>
                <span class="summary-value">
                    <span class="status-success">${currentStats.passed} 過測</span> / 
                    <span class="status-error">${currentStats.failed} 失敗</span> (共 ${currentStats.total} 步)
                </span>
            </div>
            <div class="summary-item">
                <span class="summary-label">媒體檔案</span>
                <span class="summary-value">${screenshots.length} 截圖 / ${videos.length} 影片</span>
            </div>
        </div>

        <div class="test-plan">
            <div class="test-plan-header">📋 本次測試計畫 (Test Plan)</div>
            <div class="test-plan-grid">
                <div class="test-plan-item">
                    <span class="test-plan-label">🎯 目的 (Purpose)</span>
                    <div class="test-plan-value">${reportTestPlan.purpose}</div>
                </div>
                <div class="test-plan-item">
                    <span class="test-plan-label">🛤️ 流程 (Workflow)</span>
                    <div class="test-plan-value">${reportTestPlan.workflow}</div>
                </div>
                <div class="test-plan-item">
                    <span class="test-plan-label">⚙️ 行為 (Behaviors)</span>
                    <div class="test-plan-value">${reportTestPlan.behaviors}</div>
                </div>
            </div>
        </div>

        ${aiInsight ? `
        <div class="ai-insight">
            <h3 style="margin-top: 0; color: #e67e22;">💡 AI 執行洞察</h3>
            <div style="font-size: 15px; color: #5c5c5c;">${formattedAiInsight}</div>
        </div>
        ` : ''}

        ${testCode ? `
        <div class="test-code-block">
            <div class="test-code-header">
                <span>💻 本次執行測試程式碼 (Test Code)</span>
                <span style="font-weight: normal; font-size: 12px; color: #a0aec0;">JavaScript / Playwright</span>
            </div>
            <pre style="margin: 0; padding: 15px; overflow-x: auto;"><code class="language-javascript">${testCode}</code></pre>
        </div>
        ` : ''}

        <div class="section" style="margin-top: 20px;">
            <h2>📜 執行日誌 (Terminal Logs)</h2>
            <div class="log-container">${executionLogs.join('\n') || '無日誌紀錄'}</div>
        </div>

        <div class="steps-grid">
            <div class="steps-column">
                <div class="steps-header success">✅ 已過測步驟 (${currentStats.passed})</div>
                ${enhancedSteps && enhancedSteps.filter(s => s.success).length > 0 ?
                    enhancedSteps.filter(s => s.success).map(s => `
                    <div class="step-item">
                        <div class="step-main"><span>✅</span> ${s.name}</div>
                        ${(s.behavior || s.reason) ? `
                        <div class="step-details">
                            ${s.behavior ? `<div class="step-detail-row"><span class="step-detail-label">行為:</span> <span>${s.behavior}</span></div>` : ''}
                            ${s.reason ? `<div class="step-detail-row"><span class="step-detail-label">理由:</span> <span>${s.reason}</span></div>` : ''}
                        </div>
                        ` : ''}
                    </div>`).join('') :
                    '<div class="no-data">尚未有成功的步驟</div>'}
            </div>
            <div class="steps-column">
                <div class="steps-header error">❌ 失敗步驟 (${currentStats.failed})</div>
                ${enhancedSteps && enhancedSteps.filter(s => !s.success).length > 0 ?
                    enhancedSteps.filter(s => !s.success).map(s => `
                    <div class="step-item">
                        <div class="step-main"><span>❌</span> ${s.name}</div>
                        ${(s.behavior || s.reason) ? `
                        <div class="step-details">
                            ${s.behavior ? `<div class="step-detail-row"><span class="step-detail-label">行為:</span> <span>${s.behavior}</span></div>` : ''}
                            ${s.reason ? `<div class="step-detail-row"><span class="step-detail-label">理由:</span> <span>${s.reason}</span></div>` : ''}
                        </div>
                        ` : ''}
                    </div>`).join('') :
                    '<div class="no-data">目前無失敗步驟</div>'}
            </div>
        </div>
    </div>

    <div class="section">
        <h2>📸 螢幕截圖</h2>
        ${screenshots.length > 0 ? `
        <div class="media-grid">
            ${screenshotLinks}
        </div>
        ` : '<p class="no-data">尚未擷取任何截圖</p>'}
    </div>

    <div class="section">
        <h2>🎥 錄影紀錄</h2>
        ${videos.length > 0 ? `
        <div class="media-grid">
            ${videoLinks}
        </div>
        ` : '<p class="no-data">尚未錄製任何影片</p>'}
    </div>

    <!-- Lightbox 燈箱元件 -->
    <div id="lightbox" class="lightbox" onclick="closeLightbox(event)">
        <div class="lightbox-content" onclick="event.stopPropagation()">
            <span class="lightbox-close" onclick="closeLightbox(event)">&times;</span>
            <span class="lightbox-nav lightbox-prev" onclick="changeImage(-1)">&#10094;</span>
            <img id="lightbox-img" class="lightbox-img" src="" alt="">
            <div id="lightbox-caption" class="lightbox-caption"></div>
            <div id="lightbox-counter" class="lightbox-counter"></div>
            <span class="lightbox-nav lightbox-next" onclick="changeImage(1)">&#10095;</span>
        </div>
    </div>

    <p class="timestamp">產生時間：${reportTime}</p>
    <p class="timestamp">報告目錄：${reportDir}</p>
    
    <script>
        hljs.highlightAll();

        // Gallery 燈箱邏輯
        const images = ${JSON.stringify(screenshots.map(s => ({ src: `screenshots/${s}`, name: s })))};
        let currentIndex = 0;
        let scale = 1;
        let isDragging = false;
        let startX, startY, translateX = 0, translateY = 0;

        function openLightbox(index) {
            currentIndex = index;
            resetTransform();
            updateLightbox();
            document.getElementById('lightbox').style.display = 'flex';
            document.body.style.overflow = 'hidden'; // 禁止捲動
        }

        function closeLightbox(e) {
            document.getElementById('lightbox').style.display = 'none';
            document.body.style.overflow = 'auto';
        }

        function changeImage(step) {
            currentIndex = (currentIndex + step + images.length) % images.length;
            resetTransform();
            updateLightbox();
        }

        function resetTransform() {
            scale = 1;
            translateX = 0;
            translateY = 0;
            applyTransform();
        }

        function applyTransform() {
            const img = document.getElementById('lightbox-img');
            img.style.transform = 'scale(' + scale + ') translate(' + translateX + 'px, ' + translateY + 'px)';
        }

        function updateLightbox() {
            const img = images[currentIndex];
            const lightboxImg = document.getElementById('lightbox-img');
            const lightboxCaption = document.getElementById('lightbox-caption');
            const lightboxCounter = document.getElementById('lightbox-counter');
            
            lightboxImg.src = img.src;
            lightboxCaption.textContent = img.name;
            lightboxCounter.textContent = (currentIndex + 1) + ' / ' + images.length;
        }

        // 滾輪縮放
        document.getElementById('lightbox').addEventListener('wheel', function(e) {
            e.preventDefault();
            const delta = e.deltaY > 0 ? -0.1 : 0.1;
            const newScale = Math.min(Math.max(0.5, scale + delta), 5);
            if (newScale !== scale) {
                scale = newScale;
                applyTransform();
            }
        }, { passive: false });

        // 拖動功能
        const content = document.querySelector('.lightbox-content');
        content.addEventListener('mousedown', (e) => {
            if (scale > 1) {
                isDragging = true;
                startX = e.clientX - translateX;
                startY = e.clientY - translateY;
                content.style.cursor = 'grabbing';
            }
        });

        window.addEventListener('mousemove', (e) => {
            if (isDragging) {
                translateX = e.clientX - startX;
                translateY = e.clientY - startY;
                applyTransform();
            }
        });

        window.addEventListener('mouseup', () => {
            isDragging = false;
            content.style.cursor = scale > 1 ? 'grab' : 'default';
        });

        // 鍵盤支援
        document.addEventListener('keydown', function(e) {
            if (document.getElementById('lightbox').style.display === 'flex') {
                if (e.key === 'ArrowLeft') changeImage(-1);
                if (e.key === 'ArrowRight') changeImage(1);
                if (e.key === 'Escape') closeLightbox();
            }
        });
    </script>
</body>
</html>
  `;

  const reportPath = path.join(reportDir, 'report.html');
  fs.writeFileSync(reportPath, htmlContent, 'utf8');

  // 更新「最新報告」索引
  const reportsBaseDir = path.dirname(reportDir);
  const latestReportPath = path.join(reportsBaseDir, 'latest-report.html');
  const relativeReportPath = path.relative(reportsBaseDir, reportPath).replace(/\\/g, '/');

  const latestHtml = `
<!DOCTYPE html>
<html lang="zh-TW">
<head>
    <meta charset="UTF-8">
    <meta http-equiv="refresh" content="0; url=\${relativeReportPath}">
    <title>正在重新導向至最新報告...</title>
</head>
<body>
    <p>正在重新導向至最新報告：<a href="\${relativeReportPath}">\${relativeReportPath}</a></p>
</body>
</html>
  `;
  fs.writeFileSync(latestReportPath, latestHtml, 'utf8');

  return reportPath;
}

module.exports = {
  stats,
  currentTestPlan,
  launchBrowser,
  createContext,
  createPage,
  waitForPageReady,
  safeClick,
  safeType,
  extractTexts,
  takeScreenshot,
  authenticate,
  scrollPage,
  extractTableData,
  handleCookieBanner,
  retryWithBackoff,
  detectDevServers,
  generateHtmlReport,
  initTestPlan,
  logStep,
  setGlobalStats,
  getTaipeiTimestamp,
  getExtraHeadersFromEnv
};
