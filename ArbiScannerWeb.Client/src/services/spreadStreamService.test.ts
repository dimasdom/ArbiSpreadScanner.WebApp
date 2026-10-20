import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { PossiblePositionTickerModel } from '../types/tickerType';
import { latestEventId, openSpreadStream, toEventId, type SpreadStreamHandle } from './spreadStreamService';

const getAccessTokenMock = vi.fn();

vi.mock('./loggerService', () => ({
    logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
}));
vi.mock('./oidcUserManager', () => ({ getAccessToken: () => getAccessTokenMock() }));
vi.mock('../store/services/baseQuery', () => ({ baseURL: '/api' }));

const encoder = new TextEncoder();

function ticker(id: number, spread = id) {
    return `id: ${id}\nevent: ticker\ndata: ${JSON.stringify({ spread, symbol: 'BTC/USDT' })}\n\n`;
}

const PREAMBLE = ': connected\nretry: 10\n\n';

/** A text/event-stream response. `end: false` keeps the connection open, like a live stream. */
function sseResponse(chunks: string[], { end = true, status = 200, contentType = 'text/event-stream' } = {}) {
    let controller!: ReadableStreamDefaultController<Uint8Array>;
    const body = new ReadableStream<Uint8Array>({
        start(c) {
            controller = c;
            for (const chunk of chunks) c.enqueue(encoder.encode(chunk));
            if (end) c.close();
        },
    });
    const response = new Response(body, { status, headers: { 'content-type': contentType } });
    return { response, push: (chunk: string) => controller.enqueue(encoder.encode(chunk)), end: () => controller.close() };
}

describe('spreadStreamService', () => {
    const fetchMock = vi.fn();
    const handles: SpreadStreamHandle[] = [];
    const received: PossiblePositionTickerModel[] = [];
    const onClose = vi.fn();
    const onRejected = vi.fn();

    const open = (options: { lastEventId?: string } = {}) => {
        const handle = openSpreadStream('spread-1', {
            ...options,
            retryAfterEndMs: 5,
            retryAfterErrorMs: 5,
            onTicker: (t) => received.push(t),
            onClose,
            onRejected,
        });
        handles.push(handle);
        return handle;
    };

    beforeEach(() => {
        received.length = 0;
        onClose.mockClear();
        onRejected.mockClear();
        fetchMock.mockReset();
        getAccessTokenMock.mockReset().mockResolvedValue('token-1');
        vi.stubGlobal('fetch', fetchMock);
    });

    afterEach(() => {
        handles.splice(0).forEach((h) => h.close());
        vi.unstubAllGlobals();
    });

    describe('event ids', () => {
        it('toEventId is Unix milliseconds', () => {
            expect(toEventId('2026-10-19T12:00:00Z')).toBe(String(Date.parse('2026-10-19T12:00:00Z')));
            expect(toEventId('2026-10-19T12:00:00.123Z')).toBe(String(Date.parse('2026-10-19T12:00:00.123Z')));
        });

        it('toEventId treats a timestamp without a zone as UTC, not local time', () => {
            expect(toEventId('2026-10-19T12:00:00')).toBe(toEventId('2026-10-19T12:00:00Z'));
        });

        it('toEventId keeps explicit offsets', () => {
            expect(toEventId('2026-10-19T14:00:00+02:00')).toBe(toEventId('2026-10-19T12:00:00Z'));
        });

        it('toEventId returns undefined for garbage', () => {
            expect(toEventId('not a date')).toBeUndefined();
        });

        it('latestEventId picks the newest ticker whatever the order', () => {
            const tickers = [
                { dateTime: '2026-10-19T12:00:03Z' },
                { dateTime: '2026-10-19T12:00:09Z' },
                { dateTime: '2026-10-19T12:00:01Z' },
            ];
            expect(latestEventId(tickers)).toBe(toEventId('2026-10-19T12:00:09Z'));
        });

        it('latestEventId is undefined for no tickers or unparseable ones', () => {
            expect(latestEventId([])).toBeUndefined();
            expect(latestEventId([{ dateTime: 'nope' }])).toBeUndefined();
        });
    });

    describe('request', () => {
        it('opens the spread stream with the bearer token and asks for an event stream', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE], { end: false }).response);

            open();
            await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));

            const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
            expect(url).toBe('/api/TradeOpportunity/Stream/spread-1');
            const headers = new Headers(init.headers);
            expect(headers.get('authorization')).toBe('Bearer token-1');
            expect(headers.get('accept')).toBe('text/event-stream');
            expect(headers.has('last-event-id')).toBe(false);
        });

        it('keeps the token out of the URL', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE], { end: false }).response);

            open();
            await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());

            expect(String(fetchMock.mock.calls[0][0])).not.toContain('token');
            expect(String(fetchMock.mock.calls[0][0])).not.toContain('access_token');
        });

        it('sends the resume cursor as Last-Event-ID on the first request', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE], { end: false }).response);

            open({ lastEventId: '1760875200000' });
            await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());

            expect(new Headers((fetchMock.mock.calls[0][1] as RequestInit).headers).get('last-event-id')).toBe('1760875200000');
        });

        it('omits the Authorization header when there is no token', async () => {
            getAccessTokenMock.mockResolvedValue(undefined);
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE], { end: false }).response);

            open();
            await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());

            expect(new Headers((fetchMock.mock.calls[0][1] as RequestInit).headers).has('authorization')).toBe(false);
        });

        it('encodes the spread id into the path', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE], { end: false }).response);

            openSpreadStream('a/b c', { onTicker: vi.fn(), onClose: vi.fn() });
            await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled());

            expect(fetchMock.mock.calls[0][0]).toBe('/api/TradeOpportunity/Stream/a%2Fb%20c');
        });
    });

    describe('events', () => {
        it('delivers tickers in order and ignores comments', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE, ticker(1), ': keep-alive\n\n', ticker(2)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received).toHaveLength(2));
            expect(received.map((t) => t.spread)).toEqual([1, 2]);
        });

        it('reassembles an event split across network chunks', async () => {
            const whole = ticker(7);
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE, whole.slice(0, 15), whole.slice(15)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received).toHaveLength(1));
            expect(received[0].spread).toBe(7);
        });

        it('skips a malformed event and keeps streaming', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE, 'id: 1\nevent: ticker\ndata: {oops\n\n', ticker(2)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received).toHaveLength(1));
            expect(received[0].spread).toBe(2);
        });

        it('ignores event types it does not know', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE, 'event: something-new\ndata: {}\n\n', ticker(1)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received).toHaveLength(1));
            expect(onClose).not.toHaveBeenCalled();
        });

        it('drops tickers at or before the resume cursor (already on the page)', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE, ticker(99), ticker(100), ticker(101)], { end: false }).response);

            open({ lastEventId: '100' });

            await vi.waitFor(() => expect(received).toHaveLength(1));
            expect(received[0].spread).toBe(101);
        });

        it('drops a ticker repeated after a reconnect', async () => {
            fetchMock
                .mockResolvedValueOnce(sseResponse([PREAMBLE, ticker(1), ticker(2)]).response)
                .mockResolvedValue(sseResponse([PREAMBLE, ticker(2), ticker(3)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received.map((t) => t.spread)).toEqual([1, 2, 3]));
        });
    });

    describe('closing', () => {
        it('a close event notifies once and does not reconnect', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE, ticker(1), 'event: close\ndata: {"spreadId":"x"}\n\n']).response);

            open();

            await vi.waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
            await new Promise((r) => setTimeout(r, 50));
            expect(fetchMock).toHaveBeenCalledTimes(1);
            expect(onClose).toHaveBeenCalledTimes(1);
            expect(received).toHaveLength(1);
        });

        it('events buffered behind a close event are not delivered', async () => {
            fetchMock.mockResolvedValue(sseResponse([PREAMBLE, ticker(1), 'event: close\ndata: {}\n\n', ticker(2)]).response);

            open();

            await vi.waitFor(() => expect(onClose).toHaveBeenCalled());
            await new Promise((r) => setTimeout(r, 30));
            expect(received.map((t) => t.spread)).toEqual([1]);
        });

        it('close() aborts the request and silences further events', async () => {
            const stream = sseResponse([PREAMBLE, ticker(1)], { end: false });
            fetchMock.mockResolvedValue(stream.response);
            const handle = open();
            await vi.waitFor(() => expect(received).toHaveLength(1));
            const signal = (fetchMock.mock.calls[0][1] as RequestInit).signal as AbortSignal;

            handle.close();
            expect(signal.aborted).toBe(true);
            try { stream.push(ticker(2)); } catch { /* the aborted reader may already have cancelled the stream */ }
            await new Promise((r) => setTimeout(r, 30));

            expect(received).toHaveLength(1);
            expect(fetchMock).toHaveBeenCalledTimes(1);
        });

        it('close() is idempotent and safe before the response arrives', () => {
            fetchMock.mockReturnValue(new Promise(() => {}));
            const handle = open();

            expect(() => { handle.close(); handle.close(); }).not.toThrow();
        });

        it('close() never reports a refusal or an error', async () => {
            let reject!: (e: Error) => void;
            fetchMock.mockReturnValue(new Promise((_, r) => { reject = r; }));
            const handle = open();

            handle.close();
            reject(new DOMException('aborted', 'AbortError'));
            await new Promise((r) => setTimeout(r, 30));

            expect(onRejected).not.toHaveBeenCalled();
            expect(onClose).not.toHaveBeenCalled();
        });
    });

    describe('reconnecting', () => {
        it('reconnects when the server ends a stream, resuming from the last ticker with a fresh token', async () => {
            getAccessTokenMock.mockResolvedValueOnce('token-1').mockResolvedValue('token-2');
            fetchMock
                .mockResolvedValueOnce(sseResponse([PREAMBLE, ticker(1), ticker(2)]).response) // lifetime cap: server just ends
                .mockResolvedValue(sseResponse([PREAMBLE, ticker(3)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received.map((t) => t.spread)).toEqual([1, 2, 3]));
            expect(fetchMock).toHaveBeenCalledTimes(2);
            const second = new Headers((fetchMock.mock.calls[1][1] as RequestInit).headers);
            expect(second.get('last-event-id')).toBe('2');
            expect(second.get('authorization')).toBe('Bearer token-2');
        });

        it('keeps the original cursor across a reconnect when no ticker arrived in between', async () => {
            fetchMock
                .mockResolvedValueOnce(sseResponse([PREAMBLE]).response)
                .mockResolvedValue(sseResponse([PREAMBLE], { end: false }).response);

            open({ lastEventId: '500' });

            await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
            expect(new Headers((fetchMock.mock.calls[1][1] as RequestInit).headers).get('last-event-id')).toBe('500');
        });

        it('retries after a network failure', async () => {
            fetchMock
                .mockRejectedValueOnce(new TypeError('network down'))
                .mockResolvedValue(sseResponse([PREAMBLE, ticker(1)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received).toHaveLength(1));
            expect(onRejected).not.toHaveBeenCalled();
        });

        it.each([401, 500, 502, 503])('retries after HTTP %i (token mid-renewal / restart)', async (status) => {
            fetchMock
                .mockResolvedValueOnce(new Response('', { status }))
                .mockResolvedValue(sseResponse([PREAMBLE, ticker(1)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received).toHaveLength(1));
            expect(onRejected).not.toHaveBeenCalled();
        });

        it('retries a 200 that is not an event stream (for example a proxy error page)', async () => {
            fetchMock
                .mockResolvedValueOnce(new Response('<html>', { status: 200, headers: { 'content-type': 'text/html' } }))
                .mockResolvedValue(sseResponse([PREAMBLE, ticker(1)], { end: false }).response);

            open();

            await vi.waitFor(() => expect(received).toHaveLength(1));
        });
    });

    describe('refusal', () => {
        it.each([403, 404])('HTTP %i is final: reported once and never retried', async (status) => {
            fetchMock.mockResolvedValue(new Response('', { status }));

            open();

            await vi.waitFor(() => expect(onRejected).toHaveBeenCalledWith(status));
            await new Promise((r) => setTimeout(r, 50));
            expect(onRejected).toHaveBeenCalledTimes(1);
            expect(fetchMock).toHaveBeenCalledTimes(1);
            expect(received).toHaveLength(0);
        });

        it('works without an onRejected handler', async () => {
            fetchMock.mockResolvedValue(new Response('', { status: 403 }));

            openSpreadStream('spread-1', { onTicker: vi.fn(), onClose: vi.fn() });
            await new Promise((r) => setTimeout(r, 30));

            expect(fetchMock).toHaveBeenCalledTimes(1);
        });
    });
});
