import { describe, expect, it, vi } from 'vitest';
import { coordinatedRefresh } from './refreshCoordinator';

describe('coordinatedRefresh', () => {
    it('resolves with the token returned by doRefresh', async () => {
        const doRefresh = vi.fn().mockResolvedValue('token-1');

        const token = await coordinatedRefresh(doRefresh);

        expect(token).toBe('token-1');
        expect(doRefresh).toHaveBeenCalledTimes(1);
    });

    it('queues concurrent callers and resolves them all with the single in-flight result', async () => {
        let resolveRefresh: (token: string) => void = () => {};
        const doRefresh = vi.fn(
            () =>
                new Promise<string>((resolve) => {
                    resolveRefresh = resolve;
                }),
        );

        const first = coordinatedRefresh(doRefresh);
        const second = coordinatedRefresh(doRefresh);
        const third = coordinatedRefresh(doRefresh);

        resolveRefresh('shared-token');

        await expect(first).resolves.toBe('shared-token');
        await expect(second).resolves.toBe('shared-token');
        await expect(third).resolves.toBe('shared-token');
        expect(doRefresh).toHaveBeenCalledTimes(1);
    });

    it('rejects all queued callers when the in-flight refresh fails', async () => {
        let rejectRefresh: (error: unknown) => void = () => {};
        const doRefresh = vi.fn(
            () =>
                new Promise<string>((_, reject) => {
                    rejectRefresh = reject;
                }),
        );

        const first = coordinatedRefresh(doRefresh);
        const second = coordinatedRefresh(doRefresh);

        rejectRefresh(new Error('refresh failed'));

        await expect(first).rejects.toThrow('refresh failed');
        await expect(second).rejects.toThrow('refresh failed');
    });

    it('allows a new refresh cycle to start after the previous one completes', async () => {
        const doRefresh = vi.fn().mockResolvedValueOnce('token-a').mockResolvedValueOnce('token-b');

        const tokenA = await coordinatedRefresh(doRefresh);
        const tokenB = await coordinatedRefresh(doRefresh);

        expect(tokenA).toBe('token-a');
        expect(tokenB).toBe('token-b');
        expect(doRefresh).toHaveBeenCalledTimes(2);
    });
});
