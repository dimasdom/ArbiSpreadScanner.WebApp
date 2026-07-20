import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

async function loadLogger() {
    vi.resetModules();
    const mod = await import('./loggerService');
    return mod.logger;
}

describe('loggerService', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 200 })));
    });

    afterEach(async () => {
        // The module registers its 'visibilitychange' listener on `window` at
        // import time and never removes it, so `vi.resetModules()` leaves a
        // stale listener (with its own dangling queue) attached after every
        // test. Draining any pending flush timer here — before the fake-timer
        // environment is torn down — empties that queue so the leaked
        // listener is a harmless no-op for subsequent tests.
        await vi.runOnlyPendingTimersAsync();
        vi.useRealTimers();
        vi.unstubAllGlobals();
    });

    it('does not send anything before the flush interval elapses', async () => {
        const logger = await loadLogger();

        logger.info('hello');

        expect(fetch).not.toHaveBeenCalled();
    });

    it('flushes the queued batch once the flush interval elapses', async () => {
        const logger = await loadLogger();

        logger.info('hello', 'MyComponent', 'extra detail');
        await vi.advanceTimersByTimeAsync(2000);

        expect(fetch).toHaveBeenCalledTimes(1);
        const [url, init] = vi.mocked(fetch).mock.calls[0];
        expect(String(url)).toContain('/clientlog');
        const body = JSON.parse((init as RequestInit).body as string);
        expect(body).toHaveLength(1);
        expect(body[0]).toMatchObject({
            level: 'info',
            message: 'hello',
            source: 'MyComponent',
            details: 'extra detail',
        });
    });

    it('flushes immediately on an error-level log without waiting for the timer', async () => {
        const logger = await loadLogger();

        logger.error('boom');

        expect(fetch).toHaveBeenCalledTimes(1);
    });

    it('flushes immediately once the batch reaches the max size', async () => {
        const logger = await loadLogger();

        for (let i = 0; i < 9; i++) {
            logger.info(`msg-${i}`);
        }
        expect(fetch).not.toHaveBeenCalled();

        logger.info('msg-9');

        expect(fetch).toHaveBeenCalledTimes(1);
        const [, init] = vi.mocked(fetch).mock.calls[0];
        const body = JSON.parse((init as RequestInit).body as string);
        expect(body).toHaveLength(10);
    });

    it('batches multiple entries into a single request', async () => {
        const logger = await loadLogger();

        logger.debug('one');
        logger.warn('two');
        await vi.advanceTimersByTimeAsync(2000);

        expect(fetch).toHaveBeenCalledTimes(1);
        const [, init] = vi.mocked(fetch).mock.calls[0];
        const body = JSON.parse((init as RequestInit).body as string);
        expect(body).toHaveLength(2);
        expect(body.map((e: { level: string }) => e.level)).toEqual(['debug', 'warn']);
    });

    it('does not throw when the network request fails', async () => {
        vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network down')));
        const logger = await loadLogger();

        logger.info('hello');
        await vi.advanceTimersByTimeAsync(2000);
        await vi.waitFor(() => expect(fetch).toHaveBeenCalledTimes(1));
    });

    it('flushes on visibilitychange when the tab becomes hidden', async () => {
        const logger = await loadLogger();
        logger.info('hello');
        expect(fetch).not.toHaveBeenCalled();

        Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
        window.dispatchEvent(new Event('visibilitychange'));

        expect(fetch).toHaveBeenCalledTimes(1);
    });
});
