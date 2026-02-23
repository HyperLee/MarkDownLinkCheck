#!/usr/bin/env node
/**
 * Universal Playwright Executor for Claude Code
 *
 * Executes Playwright automation code from:
 * - File path: node run.js script.js
 * - Inline code: node run.js 'await page.goto("...")'
 * - Stdin: cat script.js | node run.js
 *
 * Ensures proper module resolution by running from skill directory.
 */

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

// 擷取日誌與執行狀態
const executionLogs = [];
global.executionLogs = executionLogs; // 存入全域以供臨時腳本存取

// 預先引入 helpers，確保全域引用一致性
const helpers = require('./lib/helpers');

const originalConsoleLog = console.log;
console.log = (...args) => {
  executionLogs.push(args.join(' '));
  originalConsoleLog.apply(console, args);
};

// 產生唯一的執行 Session ID (使用台北時間)
const timestamp = helpers.getTaipeiTimestamp('filename');
const SESSION_REPORT_DIR = path.join(process.cwd(), 'playwright-reports', `run-${timestamp}`);

// 將報告路徑存入環境變數，讓 helpers 也能存取
process.env.PW_REPORT_DIR = SESSION_REPORT_DIR;

// Change to skill directory for proper module resolution if needed, 
// but we prefer to run from CWD as requested.
const SKILL_DIR = __dirname;

/**
 * Check if Playwright is installed
 */
function checkPlaywrightInstalled() {
  try {
    require.resolve('playwright');
    return true;
  } catch (e) {
    return false;
  }
}

/**
 * Install Playwright if missing
 */
function installPlaywright() {
  console.log('📦 找不到 Playwright。正在安裝...');
  try {
    execSync('npm install', { stdio: 'inherit', cwd: SKILL_DIR });
    execSync('npx playwright install chromium', { stdio: 'inherit', cwd: SKILL_DIR });
    console.log('✅ Playwright 安裝成功');
    return true;
  } catch (e) {
    console.error('❌ 安裝 Playwright 失敗：', e.message);
    console.error('請手動執行：cd', SKILL_DIR, '&& npm run setup');
    return false;
  }
}

/**
 * Get code to execute from various sources
 */
function getCodeToExecute() {
  const args = process.argv.slice(2);

  // Case 1: File path provided
  if (args.length > 0 && fs.existsSync(args[0])) {
    const filePath = path.resolve(args[0]);
    console.log(`📄 正在執行檔案：${filePath}`);
    return fs.readFileSync(filePath, 'utf8');
  }

  // Case 2: Inline code provided as argument
  if (args.length > 0) {
    console.log('⚡ 正在執行內嵌程式碼');
    return args.join(' ');
  }

  // Case 3: Code from stdin
  if (!process.stdin.isTTY) {
    console.log('📥 正在從 stdin 讀取');
    return fs.readFileSync(0, 'utf8');
  }

  // No input
  console.error('❌ 沒有可執行的程式碼');
  console.error('用法：');
  console.error('  node run.js script.js          # 執行檔案');
  console.error('  node run.js "code here"        # 執行內嵌程式碼');
  console.error('  cat script.js | node run.js    # 從 stdin 執行');
  process.exit(1);
}

/**
 * Clean up old temporary execution files from previous runs
 */
function cleanupOldTempFiles() {
  try {
    const files = fs.readdirSync(SKILL_DIR);
    const tempFiles = files.filter(f => f.startsWith('.temp-execution-') && f.endsWith('.js'));

    if (tempFiles.length > 0) {
      tempFiles.forEach(file => {
        const filePath = path.join(SKILL_DIR, file);
        try {
          fs.unlinkSync(filePath);
        } catch (e) {
          // Ignore errors - file might be in use or already deleted
        }
      });
    }
  } catch (e) {
    // Ignore directory read errors
  }
}

/**
 * Wrap code in async IIFE if not already wrapped
 */
function wrapCodeIfNeeded(code) {
  // Check if code already has require() and async structure
  const hasRequire = code.includes('require(');
  const hasAsyncIIFE = code.includes('(async () => {') || code.includes('(async()=>{');

  // If it's already a complete script, return as-is
  if (hasRequire && hasAsyncIIFE) {
    return code;
  }

  // If it's just Playwright commands, wrap in full template
  if (!hasRequire) {
    return `
const { chromium, firefox, webkit, devices } = require('playwright');
const path = require('path');
const helpers = require('${path.join(SKILL_DIR, 'lib/helpers').replace(/\\/g, '/')}');

// 測試計畫環境變數
process.env.PW_TEST_PLAN = ${JSON.stringify(process.env.PW_TEST_PLAN || '')};

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
        const rawPlan = process.env.PW_TEST_PLAN.replace(/\\\\n/g, '\\n');
        testPlan = JSON.parse(rawPlan);
      } catch (e2) {
        // 仍然失敗則使用 helpers 預設值
      }
    }

  // 取得當前執行的原始程式碼
  const rawTestCode = ${JSON.stringify(code)};

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
        ${code}
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
      const tempFileMatch = err.stack.match(/\.temp-execution-[\d]+\.js:(\d+):(\d+)/);
      if (tempFileMatch) {
        const lineNum = parseInt(tempFileMatch[1]);
        const tempFilePath = path.join('${SKILL_DIR.replace(/\\/g, '/')}', err.stack.match(/\.temp-execution-[\d]+\.js/)[0]);
        if (fs.existsSync(tempFilePath)) {
          const content = fs.readFileSync(tempFilePath, 'utf8').split('\\n');
          const start = Math.max(0, lineNum - 3);
          const end = Math.min(content.length, lineNum + 2);
          sourceCode = content.slice(start, end).map((line, idx) => {
            const currentLine = start + idx + 1;
            return currentLine + ': ' + line + (currentLine === lineNum ? ' <--- 錯誤發生在此處' : '');
          }).join('\\n');
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

    console.error('\\n❌ 自動化錯誤：' + err.message);
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
`;
  }

  // If has require but no async wrapper
  if (!hasAsyncIIFE) {
    return `
module.exports = async function runTest() {
  try {
    ${code}
  } catch (error) {
    console.error('❌ 自動化錯誤：', error.message);
    if (error.stack) {
      console.error(error.stack);
    }
    throw error;
  }
};
`;
  }

  return code;
}

/**
 * Main execution
 */
async function main() {
  console.log('🎭 Playwright 技能 - 通用執行器\n');

  // Clean up old temp files from previous runs
  cleanupOldTempFiles();

  // Check Playwright installation
  if (!checkPlaywrightInstalled()) {
    const installed = installPlaywright();
    if (!installed) {
      process.exit(1);
    }
  }

  // Get code to execute
  const rawCode = getCodeToExecute();
  const code = wrapCodeIfNeeded(rawCode);

  // Create temporary file for execution in the skill directory
  const tempFile = path.join(SKILL_DIR, `.temp-execution-${Date.now()}.js`);

  try {
    // Write code to temp file
    fs.writeFileSync(tempFile, code, 'utf8');

    // Execute the code
    console.log('🚀 開始自動化流程...\n');
    const runTest = require(tempFile);
    
    // 如果是匯出的非同步函式，則 await 執行
    if (typeof runTest === 'function') {
      await runTest();
    }

    // 更新最新報告捷徑 (latest-report.html)
    const latestReportHtml = path.join(process.cwd(), 'playwright-reports', 'latest-report.html');
    const relativePath = path.relative(path.join(process.cwd(), 'playwright-reports'), path.join(process.env.PW_REPORT_DIR, 'report.html'));
    
    const redirectHtml = `
<!DOCTYPE html>
<html lang="zh-TW">
<head>
    <meta charset="UTF-8">
    <meta http-equiv="refresh" content="0; url=${relativePath.replace(/\\/g, '/')}">
    <title>正在重新導向至最新報告...</title>
</head>
<body>
    <p>正在重新導向至最新報告：<a href="${relativePath.replace(/\\/g, '/')}">${relativePath}</a></p>
</body>
</html>
`;
    fs.writeFileSync(latestReportHtml, redirectHtml, 'utf8');

  } catch (error) {
    console.error('❌ 執行失敗：', error.message);
    if (error.stack) {
      console.error('\n📋 堆疊追蹤：');
      console.error(error.stack);
    }
    process.exit(1);
  }
}

// Run main function
main().catch(error => {
  console.error('❌ Fatal error:', error.message);
  process.exit(1);
});
