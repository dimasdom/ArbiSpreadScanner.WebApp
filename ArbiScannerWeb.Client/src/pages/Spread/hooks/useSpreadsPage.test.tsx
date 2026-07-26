import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import type { ReactNode } from 'react';
import type { GridRowParams } from '@mui/x-data-grid';

const rawBaseQueryMock = vi.fn();
const navigateMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock };
});

async function buildWrapper() {
    const { spreadApi } = await import('../../../store/services/spread');
    const store = configureStore({
        reducer: { [spreadApi.reducerPath]: spreadApi.reducer },
        middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(spreadApi.middleware),
    });
    return ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>;
}

function setViewportWidth(width: number) {
    Object.defineProperty(window, 'innerWidth', { writable: true, configurable: true, value: width });
}

describe('useSpreadsPage', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        setViewportWidth(1024);
        rawBaseQueryMock.mockResolvedValue({
            data: {
                isSuccess: true,
                value: [
                    {
                        positionModel: {
                            guid: 'g1', spread: -1.2345, type: 0, symbol: 'BTC/USDT',
                            exchangeLong: { exchange: 'okx', exchangeRate: 101, fundingRateValue: 0.001 },
                            exchangeShort: { exchange: 'binance', exchangeRate: 100, fundingRateValue: null },
                        },
                    },
                ],
            },
        });
    });

    it('maps spread rows with formatted values', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadsPage } = await import('./useSpreadsPage');

        const { result } = renderHook(() => useSpreadsPage(), { wrapper });

        await waitFor(() => expect(result.current.isLoading).toBe(false));
        expect(result.current.rows).toHaveLength(1);
        expect(result.current.rows[0]).toMatchObject({
            id: 'g1',
            spread: '1.2345',
            exchangeLong: 'okx',
            exchangeShort: 'binance',
            exchangeRateLong: '101.000000',
            exchangeRateShort: '100.000000',
        });
    });

    it('reports isError when the query fails', async () => {
        rawBaseQueryMock.mockResolvedValue({ error: { status: 500, data: 'boom' } });
        const wrapper = await buildWrapper();
        const { useSpreadsPage } = await import('./useSpreadsPage');

        const { result } = renderHook(() => useSpreadsPage(), { wrapper });

        await waitFor(() => expect(result.current.isError).toBe(true));
    });

    it('handleRowDoubleClick navigates to the spread detail page', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadsPage } = await import('./useSpreadsPage');
        const { result } = renderHook(() => useSpreadsPage(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        result.current.handleRowDoubleClick({ row: { guid: 'g1' } } as GridRowParams);

        expect(navigateMock).toHaveBeenCalledWith('/en/spread?id=g1', undefined);
    });

    it('handleRowClick navigates only on mobile', async () => {
        setViewportWidth(1024);
        const wrapper = await buildWrapper();
        const { useSpreadsPage } = await import('./useSpreadsPage');
        const { result } = renderHook(() => useSpreadsPage(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        result.current.handleRowClick({ row: { guid: 'g1' } } as GridRowParams);
        expect(navigateMock).not.toHaveBeenCalled();
    });

    it('handleRowClick navigates when on a mobile viewport', async () => {
        setViewportWidth(400);
        const wrapper = await buildWrapper();
        const { useSpreadsPage } = await import('./useSpreadsPage');
        const { result } = renderHook(() => useSpreadsPage(), { wrapper });
        await waitFor(() => expect(result.current.isMobile).toBe(true));

        result.current.handleRowClick({ row: { guid: 'g1' } } as GridRowParams);

        expect(navigateMock).toHaveBeenCalledWith('/en/spread?id=g1', undefined);
    });
});
