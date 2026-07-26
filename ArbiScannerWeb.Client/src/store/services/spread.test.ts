import { beforeEach, describe, expect, it, vi } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';
import type { TradeOpportunityDetailsDTO } from '../../types/tradeOpportunityModel';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

async function buildStore() {
    const { spreadApi } = await import('./spread');
    return {
        spreadApi,
        store: configureStore({
            reducer: { [spreadApi.reducerPath]: spreadApi.reducer },
            middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(spreadApi.middleware),
        }),
    };
}

describe('spreadApi', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
    });

    it('getSpreads requests the expected URL and maps details to position models', async () => {
        const details: TradeOpportunityDetailsDTO[] = [
            { positionModel: { guid: 'g1' } } as unknown as TradeOpportunityDetailsDTO,
            { positionModel: { guid: 'g2' } } as unknown as TradeOpportunityDetailsDTO,
        ];
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: details } });
        const { spreadApi, store } = await buildStore();

        const result = await store.dispatch(spreadApi.endpoints.getSpreads.initiate());

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/TradeOpportunity/GetSpreadsForUser', method: 'GET' });
        expect(result.data?.value).toEqual([{ guid: 'g1' }, { guid: 'g2' }]);
    });

    it('getSpreads maps an absent value to an empty array', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, value: undefined } });
        const { spreadApi, store } = await buildStore();

        const result = await store.dispatch(spreadApi.endpoints.getSpreads.initiate());

        expect(result.data?.value).toEqual([]);
    });

    it('getSpreadInfo requests the expected URL for a given guid', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: {} } });
        const { spreadApi, store } = await buildStore();

        await store.dispatch(spreadApi.endpoints.getSpreadInfo.initiate('abc-123'));

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/TradeOpportunity/GetSpreadInfo/abc-123', method: 'GET' });
    });
});
