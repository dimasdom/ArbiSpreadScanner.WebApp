import { baseURL } from "../store/services/baseQuery";

type LogLevel = 'info' | 'warn' | 'error' | 'debug';

interface LogEntry {
    level: LogLevel;
    message: string;
    source?: string;
    timestamp: string;
    details?: string;
}

const FLUSH_INTERVAL_MS = 2000;
const MAX_BATCH_SIZE = 10;
const ENDPOINT = `${baseURL}/clientlog`;

const queue: LogEntry[] = [];
let flushTimer: ReturnType<typeof setTimeout> | null = null;

function scheduleFlush() {
    if (flushTimer !== null) return;
    flushTimer = setTimeout(() => {
        flushTimer = null;
        sendBatch();
    }, FLUSH_INTERVAL_MS);
}

function sendBatch() {
    if (queue.length === 0) return;
    const batch = queue.splice(0, queue.length);
    fetch(ENDPOINT, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(batch),
        keepalive: true,
    }).catch(() => {
        // Silently discard — logging must never cause app errors
    });
}

function enqueue(level: LogLevel, message: string, source?: string, details?: string) {
    queue.push({
        level,
        message,
        source,
        timestamp: new Date().toISOString(),
        details,
    });

    if (level === 'error' || queue.length >= MAX_BATCH_SIZE) {
        if (flushTimer !== null) {
            clearTimeout(flushTimer);
            flushTimer = null;
        }
        sendBatch();
    } else {
        scheduleFlush();
    }
}

// Flush remaining entries when the tab is closed
window.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') sendBatch();
});

export const logger = {
    info: (message: string, source?: string, details?: string) =>
        enqueue('info', message, source, details),
    warn: (message: string, source?: string, details?: string) =>
        enqueue('warn', message, source, details),
    error: (message: string, source?: string, details?: string) =>
        enqueue('error', message, source, details),
    debug: (message: string, source?: string, details?: string) =>
        enqueue('debug', message, source, details),
};
