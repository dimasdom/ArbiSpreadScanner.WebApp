import { fetchEventSource } from '@microsoft/fetch-event-source';
import { baseURL } from '../store/services/baseQuery';
import type { PossiblePositionTickerModel } from '../types/tickerType';
import { logger } from './loggerService';
import { getAccessToken } from './oidcUserManager';

// Live ticker stream of one spread over Server-Sent Events. A fetch-based client rather than the browser's EventSource
// because EventSource cannot send an Authorization header; this also keeps the access token out of the URL (and so out of
// proxy logs), and lets every reconnect pick up a freshly renewed token.

const DEFAULT_RETRY_AFTER_END_MS = 500;

export interface SpreadStreamOptions {
    /** Unix-ms id of the newest ticker the page already has; the server replays whatever came after it. */
    lastEventId?: string;
    onTicker: (ticker: PossiblePositionTickerModel) => void;
    /** The spread was closed by the scanner; the stream is finished and will not reconnect. */
    onClose: () => void;
    /** The server refused the stream for good (403 no subscription, 404 unknown spread); it will not retry. */
    onRejected?: (status: number) => void;
    /** How long to wait before reconnecting after the server ends a healthy stream (its lifetime cap). */
    retryAfterEndMs?: number;
    /** How long to wait before retrying after a failure. Defaults to the interval the server advertises (3 s). */
    retryAfterErrorMs?: number;
}

export interface SpreadStreamHandle {
    close: () => void;
}

class RejectedError extends Error {
    readonly status: number;

    constructor(status: number) {
        super(`Spread stream rejected with HTTP ${status}`);
        this.status = status;
    }
}

class StreamEndedError extends Error {
    constructor() {
        super('Spread stream ended');
    }
}

/** The SSE id the server uses for a ticker: Unix milliseconds of its timestamp. */
export function toEventId(dateTime: string): string | undefined {
    // The REST API sends UTC timestamps; one without a zone suffix would otherwise be read as local time.
    const hasZone = /(Z|[+-]\d{2}:?\d{2})$/i.test(dateTime);
    const ms = Date.parse(hasZone ? dateTime : `${dateTime}Z`);
    return Number.isNaN(ms) ? undefined : String(ms);
}

/** Resume cursor for a list of tickers in any order: the id of the newest one. */
export function latestEventId(tickers: Pick<PossiblePositionTickerModel, 'dateTime'>[]): string | undefined {
    let newest: number | undefined;
    for (const ticker of tickers) {
        const id = toEventId(ticker.dateTime);
        if (id !== undefined && (newest === undefined || Number(id) > newest)) newest = Number(id);
    }
    return newest === undefined ? undefined : String(newest);
}

async function authorizedFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    const token = await getAccessToken();
    const headers = new Headers(init?.headers);
    if (token) headers.set('Authorization', `Bearer ${token}`);
    return globalThis.fetch(input, { ...init, headers });
}

export function openSpreadStream(spreadId: string, options: SpreadStreamOptions): SpreadStreamHandle {
    const controller = new AbortController();
    const retryAfterEndMs = options.retryAfterEndMs ?? DEFAULT_RETRY_AFTER_END_MS;
    let finished = false;
    // Events are only ever newer than this; guards against the same ticker arriving twice around a reconnect.
    let lastTickerId = options.lastEventId === undefined ? -Infinity : Number(options.lastEventId);

    const finish = () => {
        finished = true;
        controller.abort();
    };

    fetchEventSource(`${baseURL}/TradeOpportunity/Stream/${encodeURIComponent(spreadId)}`, {
        signal: controller.signal,
        // The library resends the newest id it has seen on every reconnect, overwriting this same (lowercase) key.
        headers: options.lastEventId === undefined ? {} : { 'last-event-id': options.lastEventId },
        // A backgrounded tab keeps its stream; closing it would trade a held connection for a replay on return.
        openWhenHidden: true,
        fetch: authorizedFetch,

        async onopen(response) {
            if (response.ok && response.headers.get('content-type')?.startsWith('text/event-stream')) return;
            if (response.status === 403 || response.status === 404) throw new RejectedError(response.status);
            // 401 is usually a token mid-renewal, 5xx a restart: both are worth retrying.
            throw new Error(`Spread stream failed to open: HTTP ${response.status}`);
        },

        onmessage(message) {
            // Events already buffered when the stream was closed (by the caller or by a `close` event) must not leak out.
            if (finished) return;

            if (message.event === 'close') {
                finish();
                options.onClose();
                return;
            }

            if (message.event !== 'ticker') return;

            const id = message.id ? Number(message.id) : undefined;
            if (id !== undefined && id <= lastTickerId) return;

            let ticker: PossiblePositionTickerModel;
            try {
                ticker = JSON.parse(message.data) as PossiblePositionTickerModel;
            } catch {
                logger.warn('Dropped a malformed spread stream event', 'spreadStreamService');
                return;
            }

            if (id !== undefined) lastTickerId = id;
            options.onTicker(ticker);
        },

        onclose() {
            // The library does not reconnect after a clean end unless this throws. The server ends healthy streams on
            // purpose (token-lifetime cap), so reconnect, unless the spread closed or the caller went away.
            if (!finished) throw new StreamEndedError();
        },

        onerror(error) {
            if (error instanceof RejectedError) throw error; // stops the library's retry loop
            if (error instanceof StreamEndedError) return retryAfterEndMs;

            logger.warn(`Spread stream interrupted, retrying: ${error instanceof Error ? error.message : String(error)}`, 'spreadStreamService');
            return options.retryAfterErrorMs; // undefined: the library's interval, which the server sets to 3 s
        },
    }).catch((error: unknown) => {
        if (controller.signal.aborted) return;

        if (error instanceof RejectedError) {
            logger.warn(`Spread stream refused (HTTP ${error.status})`, 'spreadStreamService');
            options.onRejected?.(error.status);
            return;
        }

        logger.error('Spread stream failed', 'spreadStreamService', error instanceof Error ? error.message : String(error));
    });

    return { close: () => { finished = true; controller.abort(); } };
}
