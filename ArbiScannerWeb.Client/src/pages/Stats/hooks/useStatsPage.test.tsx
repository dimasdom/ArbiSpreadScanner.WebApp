import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import type { ReactNode } from 'react';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

// Newest first, matching the order the real API returns.
const indexEntries = [
    { id: 'snap-3', generatedAtUtc: '2026-01-03T00:00:00Z' },
    { id: 'snap-2', generatedAtUtc: '2026-01-02T00:00:00Z' },
    { id: 'snap-1', generatedAtUtc: '2026-01-01T00:00:00Z' },
];

function mockHappyPath() {
    rawBaseQueryMock.mockImplementation((args: { url: string }) => {
        if (args.url === '/SpreadStats/GetSnapshotIndex') {
            return Promise.resolve({ data: { isSuccess: true, value: indexEntries } });
        }
        if (args.url === '/SpreadStats/GetLatest') {
            return Promise.resolve({
                data: { isSuccess: true, value: { id: 'snap-3', generatedAtUtc: '2026-01-03T00:00:00Z', totalSpreadsAnalyzed: 3 } },
            });
        }
        if (args.url.startsWith('/SpreadStats/GetById')) {
            const id = new URLSearchParams(args.url.split('?')[1]).get('id');
            const entry = indexEntries.find((e) => e.id === id);
            return Promise.resolve({ data: { isSuccess: true, value: { id, generatedAtUtc: entry?.generatedAtUtc, totalSpreadsAnalyzed: 1 } } });
        }
        return Promise.resolve({ data: { isSuccess: false, value: undefined } });
    });
}

async function buildWrapper() {
    const { statsApi } = await import('../../../store/services/stats');
    const store = configureStore({
        reducer: { [statsApi.reducerPath]: statsApi.reducer },
        middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(statsApi.middleware),
    });
    return ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>;
}

describe('useStatsPage', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        mockHappyPath();
    });

    it('defaults to viewing the latest snapshot, with the select pointing at the newest record', async () => {
        const wrapper = await buildWrapper();
        const { useStatsPage } = await import('./useStatsPage');

        const { result } = renderHook(() => useStatsPage(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        expect(result.current.isViewingLatest).toBe(true);
        expect(result.current.snapshot?.id).toBe('snap-3');
        expect(result.current.hasAnySnapshot).toBe(true);
        expect(result.current.snapshots).toEqual(indexEntries);
        expect(result.current.selectedSnapshotId).toBe('snap-3');
    });

    it('switches to the chosen snapshot by id', async () => {
        const wrapper = await buildWrapper();
        const { useStatsPage } = await import('./useStatsPage');
        const { result } = renderHook(() => useStatsPage(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        act(() => result.current.handleSnapshotChange('snap-2'));

        await waitFor(() => expect(result.current.isViewingLatest).toBe(false));
        expect(result.current.selectedSnapshotId).toBe('snap-2');
        await waitFor(() => expect(result.current.snapshot?.id).toBe('snap-2'));
    });

    it('resets back to the latest snapshot when an empty id is passed', async () => {
        const wrapper = await buildWrapper();
        const { useStatsPage } = await import('./useStatsPage');
        const { result } = renderHook(() => useStatsPage(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));
        act(() => result.current.handleSnapshotChange('snap-1'));
        await waitFor(() => expect(result.current.isViewingLatest).toBe(false));

        act(() => result.current.handleSnapshotChange(''));

        expect(result.current.isViewingLatest).toBe(true);
        expect(result.current.selectedSnapshotId).toBe('snap-3');
        await waitFor(() => expect(result.current.snapshot?.id).toBe('snap-3'));
    });

    it('reports no snapshot and an error when nothing has been generated yet', async () => {
        rawBaseQueryMock.mockImplementation((args: { url: string }) => {
            if (args.url === '/SpreadStats/GetSnapshotIndex') {
                return Promise.resolve({ data: { isSuccess: true, value: [] } });
            }
            if (args.url === '/SpreadStats/GetLatest') {
                return Promise.resolve({ error: { status: 404, data: 'not found' } });
            }
            return Promise.resolve({ data: { isSuccess: false, value: undefined } });
        });
        const wrapper = await buildWrapper();
        const { useStatsPage } = await import('./useStatsPage');

        const { result } = renderHook(() => useStatsPage(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        expect(result.current.hasAnySnapshot).toBe(false);
        expect(result.current.isError).toBe(true);
        expect(result.current.snapshot).toBeUndefined();
        expect(result.current.selectedSnapshotId).toBe('');
    });
});
