// Markdown Link Check - SSE Client Implementation

let controller = null; // AbortController for cancellation
let currentReport = null; // Store current report for copy functionality

document.addEventListener('DOMContentLoaded', function () {
    const modeMarkdown = document.getElementById('modeMarkdown');
    const modeRepo = document.getElementById('modeRepo');
    const markdownSection = document.getElementById('markdownSourceSection');
    const repoSection = document.getElementById('repoUrlSection');
    const markdownContent = document.getElementById('markdownContent');
    const charCount = document.getElementById('charCount');
    const startButton = document.getElementById('startCheck');
    const cancelButton = document.getElementById('cancelCheck');
    const copyButton = document.getElementById('copyMarkdown');

    // Mode switching
    modeMarkdown.addEventListener('change', function () {
        if (this.checked) {
            markdownSection.style.display = 'block';
            repoSection.style.display = 'none';
        }
    });

    modeRepo.addEventListener('change', function () {
        if (this.checked) {
            markdownSection.style.display = 'none';
            repoSection.style.display = 'block';
        }
    });

    // Character count
    markdownContent.addEventListener('input', function () {
        charCount.textContent = this.value.length;
        if (this.value.length > 100000) {
            charCount.classList.add('text-danger');
        } else {
            charCount.classList.remove('text-danger');
        }
    });

    // Start check button
    startButton.addEventListener('click', async function () {
        const mode = document.querySelector('input[name="mode"]:checked').value;
        let payload = { mode };

        if (mode === 'MarkdownSource') {
            const content = markdownContent.value.trim();
            if (!content) {
                showError('請輸入 Markdown 內容');
                return;
            }
            if (content.length > 100000) {
                showError('Markdown 內容長度不可超過 100,000 字元');
                return;
            }
            payload.markdownContent = content;
        } else {
            const repoUrl = document.getElementById('repoUrl').value.trim();
            if (!repoUrl) {
                showError('請輸入 GitHub Repository URL');
                return;
            }
            if (!repoUrl.match(/^https:\/\/github\.com\/[a-zA-Z0-9\-_.]+\/[a-zA-Z0-9\-_.]+$/)) {
                showError('請輸入合法的 GitHub Repository URL');
                return;
            }
            payload.repoUrl = repoUrl;
            const branch = document.getElementById('branch').value.trim();
            if (branch) {
                payload.branch = branch;
            }
        }

        await startCheck(payload);
    });

    // Cancel button
    cancelButton.addEventListener('click', function () {
        if (controller) {
            controller.abort();
            controller = null;
            hideProgress();
            showError('檢測已取消');
        }
    });

    // Copy as Markdown button
    copyButton.addEventListener('click', async function () {
        if (!currentReport) {
            showError('沒有可複製的報告');
            return;
        }

        const markdown = generateMarkdownReport(currentReport);
        try {
            await navigator.clipboard.writeText(markdown);
            showToast('已複製到剪貼簿');
        } catch (err) {
            showError('複製失敗：' + err.message);
        }
    });
});

async function startCheck(payload) {
    // Reset UI
    hideError();
    document.getElementById('fileResults').innerHTML = '';
    document.getElementById('reportSummary').style.display = 'none';
    currentReport = null;

    // Show progress
    showProgress();
    document.getElementById('startCheck').style.display = 'none';
    document.getElementById('cancelCheck').style.display = 'block';

    // Create AbortController for cancellation
    controller = new AbortController();

    try {
        const response = await fetch('/api/check/sse', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
            signal: controller.signal
        });

        if (!response.ok) {
            if (response.status === 400) {
                const error = await response.json();
                throw new Error(error.message || '請求驗證失敗');
            } else if (response.status === 429) {
                const error = await response.json();
                throw new Error(error.message || '請求過於頻繁，請稍後再試');
            } else {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const events = parseSSE(buffer);
            buffer = events.remaining;

            for (const event of events.parsed) {
                handleSSE(event);
            }
        }
    } catch (err) {
        if (err.name === 'AbortError') {
            // User cancelled
            return;
        }
        showError('檢測失敗：' + err.message);
    } finally {
        hideProgress();
        document.getElementById('startCheck').style.display = 'block';
        document.getElementById('cancelCheck').style.display = 'none';
        controller = null;
    }
}

function parseSSE(buffer) {
    const events = [];
    const lines = buffer.split('\n');
    let remaining = '';
    let currentEvent = { type: '', data: '' };

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];

        if (line.startsWith('event:')) {
            currentEvent.type = line.substring(6).trim();
        } else if (line.startsWith('data:')) {
            currentEvent.data += line.substring(5).trim();
        } else if (line === '') {
            // Event complete
            if (currentEvent.type && currentEvent.data) {
                try {
                    events.push({
                        type: currentEvent.type,
                        data: JSON.parse(currentEvent.data)
                    });
                } catch (e) {
                    console.error('Failed to parse SSE data:', currentEvent.data, e);
                }
            }
            currentEvent = { type: '', data: '' };
        } else if (i === lines.length - 1) {
            // Last line (incomplete)
            remaining = line;
        }
    }

    return { parsed: events, remaining };
}

function handleSSE(event) {
    switch (event.type) {
        case 'progress':
            updateProgress(event.data);
            break;
        case 'file-result':
            displayFileResult(event.data.fileResult);
            break;
        case 'complete':
            displayReport(event.data.report);
            break;
        case 'error':
            showError(event.data.errorMessage);
            break;
    }
}

function updateProgress(data) {
    const { checkedCount, totalCount, currentFile } = data;
    const percentage = totalCount > 0 ? (checkedCount / totalCount * 100) : 0;
    document.getElementById('progressBar').style.width = percentage + '%';
    document.getElementById('progressText').textContent = 
        `已檢查 ${checkedCount} / ${totalCount} 個連結${currentFile ? ` | 正在處理：${currentFile}` : ''}`;
}

function displayFileResult(fileResult) {
    const container = document.getElementById('fileResults');
    const fileCard = document.createElement('div');
    fileCard.className = 'card mb-3';
    
    const statusClass = fileResult.brokenCount > 0 ? 'border-danger' : 
                       fileResult.warningCount > 0 ? 'border-warning' : 'border-success';
    fileCard.classList.add(statusClass);

    let html = `
        <div class="card-header bg-light">
            <h6 class="mb-0">
                📄 ${escapeHtml(fileResult.fileName || fileResult.relativePath)}
                <span class="badge bg-success">${fileResult.healthyCount} ✅</span>
                <span class="badge bg-danger">${fileResult.brokenCount} ❌</span>
                <span class="badge bg-warning text-dark">${fileResult.warningCount} ⚠️</span>
                <span class="badge bg-secondary">${fileResult.skippedCount} ⏭️</span>
            </h6>
        </div>
        <div class="card-body">
            <table class="table table-sm table-hover">
                <thead>
                    <tr>
                        <th style="width: 50px;">狀態</th>
                        <th>目標 URL</th>
                        <th style="width: 80px;">行號</th>
                        <th style="width: 150px;">錯誤訊息</th>
                    </tr>
                </thead>
                <tbody>
    `;

    for (const result of fileResult.linkResults) {
        const statusIcon = getStatusIcon(result.status);
        const statusClass = getStatusClass(result.status);
        const errorMsg = result.errorMessage || '';
        const suggestion = result.anchorSuggestion ? ` (建議：${result.anchorSuggestion})` : '';

        html += `
            <tr class="${statusClass}">
                <td class="text-center">${statusIcon}</td>
                <td><code>${escapeHtml(result.targetUrl)}</code></td>
                <td class="text-center">${result.lineNumber || '-'}</td>
                <td>${escapeHtml(errorMsg + suggestion)}</td>
            </tr>
        `;
    }

    html += `
                </tbody>
            </table>
        </div>
    `;

    fileCard.innerHTML = html;
    container.appendChild(fileCard);
}

function displayReport(report) {
    currentReport = report;

    document.getElementById('summaryFileCount').textContent = report.fileCount;
    document.getElementById('summaryTotal').textContent = report.totalLinkCount;
    document.getElementById('summaryHealthy').textContent = report.healthyCount;
    document.getElementById('summaryBroken').textContent = report.brokenCount;
    document.getElementById('summaryWarning').textContent = report.warningCount;
    document.getElementById('summarySkipped').textContent = report.skippedCount;
    document.getElementById('summaryDuration').textContent = formatDuration(report.totalDuration);

    document.getElementById('reportSummary').style.display = 'block';
}

function generateMarkdownReport(report) {
    let markdown = `# Markdown Link Check Report\n\n`;
    markdown += `**檢測時間**: ${new Date().toLocaleString()}\n\n`;
    markdown += `## 摘要\n\n`;
    markdown += `| 項目 | 數量 |\n`;
    markdown += `|------|-----:|\n`;
    markdown += `| 檔案數量 | ${report.fileCount} |\n`;
    markdown += `| 連結總數 | ${report.totalLinkCount} |\n`;
    markdown += `| ✅ 正常連結 | ${report.healthyCount} |\n`;
    markdown += `| ❌ 失效連結 | ${report.brokenCount} |\n`;
    markdown += `| ⚠️ 警告 | ${report.warningCount} |\n`;
    markdown += `| ⏭️ 跳過 | ${report.skippedCount} |\n`;
    markdown += `| 總耗時 | ${formatDuration(report.totalDuration)} |\n\n`;

    if (report.fileResults && report.fileResults.length > 0) {
        markdown += `## 詳細結果\n\n`;
        
        for (const fileResult of report.fileResults) {
            markdown += `### 📄 ${fileResult.fileName || fileResult.relativePath}\n\n`;
            markdown += `| 狀態 | 目標 URL | 行號 | 錯誤訊息 |\n`;
            markdown += `|:----:|----------|:----:|----------|\n`;

            for (const result of fileResult.linkResults) {
                const statusIcon = getStatusIcon(result.status);
                const errorMsg = result.errorMessage || '-';
                const suggestion = result.anchorSuggestion ? ` (建議：${result.anchorSuggestion})` : '';
                
                markdown += `| ${statusIcon} | \`${result.targetUrl}\` | ${result.lineNumber || '-'} | ${errorMsg}${suggestion} |\n`;
            }
            
            markdown += `\n`;
        }
    }

    return markdown;
}

function getStatusIcon(status) {
    switch (status) {
        case 'Healthy': return '✅';
        case 'Broken': return '❌';
        case 'Warning': return '⚠️';
        case 'Skipped': return '⏭️';
        default: return '❓';
    }
}

function getStatusClass(status) {
    switch (status) {
        case 'Broken': return 'table-danger';
        case 'Warning': return 'table-warning';
        case 'Skipped': return 'table-secondary';
        default: return '';
    }
}

function formatDuration(duration) {
    if (typeof duration === 'string') {
        // Parse TimeSpan string format "HH:mm:ss.fffffff" or "mm:ss.fffffff"
        const parts = duration.split(':');
        let totalSeconds = 0;
        if (parts.length === 3) {
            totalSeconds = parseInt(parts[0]) * 3600 + parseInt(parts[1]) * 60 + parseFloat(parts[2]);
        } else if (parts.length === 2) {
            totalSeconds = parseInt(parts[0]) * 60 + parseFloat(parts[1]);
        } else {
            totalSeconds = parseFloat(duration) || 0;
        }
        if (totalSeconds < 1) return `${Math.round(totalSeconds * 1000)}ms`;
        return `${totalSeconds.toFixed(1)}s`;
    }
    if (typeof duration === 'number') {
        if (duration < 1000) return `${duration}ms`;
        return `${(duration / 1000).toFixed(1)}s`;
    }
    return '0ms';
}

function escapeHtml(unsafe) {
    if (!unsafe) return '';
    return unsafe
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#039;");
}

function showProgress() {
    document.getElementById('progressSection').style.display = 'block';
    document.getElementById('progressBar').style.width = '0%';
    document.getElementById('progressText').textContent = '準備中...';
}

function hideProgress() {
    document.getElementById('progressSection').style.display = 'none';
}

function showError(message) {
    const errorDiv = document.getElementById('errorMessage');
    errorDiv.textContent = message;
    errorDiv.style.display = 'block';
    setTimeout(() => {
        errorDiv.style.display = 'none';
    }, 10000);
}

function hideError() {
    document.getElementById('errorMessage').style.display = 'none';
}

function showToast(message) {
    // Simple toast notification
    const toast = document.createElement('div');
    toast.className = 'alert alert-success position-fixed bottom-0 end-0 m-3';
    toast.textContent = message;
    toast.style.zIndex = '9999';
    document.body.appendChild(toast);
    
    setTimeout(() => {
        toast.remove();
    }, 3000);
}

