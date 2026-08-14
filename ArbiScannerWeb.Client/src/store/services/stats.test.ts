import { beforeEach, describe, expect, it, vi } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

async function buildStore() {
    const { statsApi } = await import('./stats');
    return {
        statsApi,
        store: configureStore({
            reducer: { [statsApi.reducerPath]: statsApi.reducer },
            middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(statsApi.middleware),
        }),
    };
}

describe('statsApi', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
    });

    it('getLatestStats requests the expected URL', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: {} } });
        const { statsApi, store } = await buildStore();

        await store.dispatch(statsApi.endpoints.getLatestStats.initiate());

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/SpreadStats/GetLatest', method: 'GET' });
    });

    it('getStatsById requests the expected URL for a given id', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: {} } });
        const { statsApi, store } = await buildStore();

        await store.dispatch(statsApi.endpoints.getStatsById.initiate('abc-123'));

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/SpreadStats/GetById?id=abc-123', method: 'GET' });
    });

    it('getSnapshotIndex requests the expected URL', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: [] } });
        const { statsApi, store } = await buildStore();

        await store.dispatch(statsApi.endpoints.getSnapshotIndex.initiate());

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/SpreadStats/GetSnapshotIndex', method: 'GET' });
    });
});
