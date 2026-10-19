import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import type { ReactNode } from 'react';
import accountReducer from '../../../store/slices/accountSlice';
import { createEmptyAccountModel } from '../../../types/accountType';
import type { PossiblePositionTickerModel } from '../../../types/tickerType';
import { ChatContextProvider } from '../../../contexts/ChatContext';

const rawBaseQueryMock = vi.fn();
const navigateMock = vi.fn();
let searchParams = new URLSearchParams('id=abc-123');

const streamHandle = { close: vi.fn() };
const openSpreadStreamMock = vi.fn<(spreadId: string, options: StreamOptions) => typeof streamHandle>(() => streamHandle);

type StreamOptions = {
    lastEventId?: string;
    onTicker: (ticker: PossiblePositionTickerModel) => void;
    onClose: () => void;
    onRejected?: (status: number) => void;
};

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock, useSearchParams: () => [searchParams, vi.fn()] };
});

vi.mock('../../../services/spreadStreamService', async (importOriginal) => {
    const actual = await importOriginal<typeof import('../../../services/spreadStreamService')>();
    return { ...actual, openSpreadStream: openSpreadStreamMock };
});

function buildSpreadDetails(overrides: Partial<Record<string, unknown>> = {}) {
    return {
        positionModel: {
            guid: 'abc-123',
            spread: 1.5,
            type: 0,
            symbol: 'BTC/USDT',
            exchangeShort: { exchange: 'binance', exchangeRate: 100 },
            exchangeLong: { exchange: 'okx', exchangeRate: 101 },
            exchangeRateA: { exchange: 'binance' },
            exchangeRateB: { exchange: 'okx' },
        },
        tickers: [],
        groupName: 'group-abc-123',
        shortExchangeUrl: null,
        longExchangeUrl: null,
        ...overrides,
    };
}

async function buildWrapper() {
    const { spreadApi } = await import('../../../store/services/spread');
    const store = configureStore({
        reducer: { account: accountReducer, [spreadApi.reducerPath]: spreadApi.reducer },
        middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(spreadApi.middleware),
        preloadedState: {
            account: {
                account: { ...createEmptyAccountModel(), userSettings: { ...createEmptyAccountModel().userSettings, positionSize: 10 } },
                isLoggedIn: true, loading: false, error: null, emailConfirmToken: null,
                needsEmailConfirmation: false, sessionChecked: true,
            },
        },
    });
    return ({ children }: { children: ReactNode }) => (
        <Provider store={store}>
            <ChatContextProvider>{children}</ChatContextProvider>
        </Provider>
    );
}

describe('useSpreadPage', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        searchParams = new URLSearchParams('id=abc-123');
        openSpreadStreamMock.mockClear();
        streamHandle.close.mockClear();
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: buildSpreadDetails() } });
    });

    it('navigates to /spreads when there is no spread id', async () => {
        searchParams = new URLSearchParams();
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/en/spreads', undefined));
    });

    it('loads the spread and opens the live stream for it', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        const { result } = renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(result.current.isLoading).toBe(false));
        expect(result.current.pos?.guid).toBe('abc-123');
        await waitFor(() => expect(openSpreadStreamMock).toHaveBeenCalledTimes(1));
        expect(openSpreadStreamMock.mock.calls[0][0]).toBe('abc-123');
    });

    it('does not open the stream before the spread has loaded', async () => {
        rawBaseQueryMock.mockResolvedValue({ error: { status: 404, data: 'not found' } });
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(navigateMock).toHaveBeenCalled());
        expect(openSpreadStreamMock).not.toHaveBeenCalled();
    });

    it('resumes the stream from the newest ticker of the REST snapshot', async () => {
        const older = { dateTime: '2026-10-19T12:00:00Z' };
        const newest = { dateTime: '2026-10-19T12:00:05Z' };
        rawBaseQueryMock.mockResolvedValue({
            data: { isSuccess: true, value: buildSpreadDetails({ tickers: [newest, older] }) },
        });
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(openSpreadStreamMock).toHaveBeenCalled());
        expect(openSpreadStreamMock.mock.calls[0][1].lastEventId).toBe(String(Date.parse('2026-10-19T12:00:05Z')));
    });

    it('starts without a resume cursor when the snapshot has no tickers', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(openSpreadStreamMock).toHaveBeenCalled());
        expect(openSpreadStreamMock.mock.calls[0][1].lastEventId).toBeUndefined();
    });

    it('navigates away when the spread query errors', async () => {
        rawBaseQueryMock.mockResolvedValue({ error: { status: 404, data: 'not found' } });
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/en/spreads', undefined));
    });

    it('appends an incoming ticker to the ticker list', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { result } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(openSpreadStreamMock).toHaveBeenCalled());
        const { onTicker } = openSpreadStreamMock.mock.calls[0][1];

        act(() => {
            onTicker({ echangeA: 'binance', exchangeLong: 'okx', exchangeShort: 'binance', rateA: 100, rateB: 101, spread: 1 } as never);
        });
        act(() => {
            onTicker({ echangeA: 'binance', exchangeLong: 'okx', exchangeShort: 'binance', rateA: 102, rateB: 103, spread: 2 } as never);
        });

        expect(result.current.tickers).toHaveLength(2);
        expect(result.current.spreadVal).toBe(2);
    });

    it('opens the closed-dialog when the spread closes', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { result } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(openSpreadStreamMock).toHaveBeenCalled());
        expect(result.current.isSpreadClosedDialogOpen).toBe(false);

        act(() => { openSpreadStreamMock.mock.calls[0][1].onClose(); });

        expect(result.current.isSpreadClosedDialogOpen).toBe(true);
    });

    it('stays on the page when live updates are refused', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { result } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(openSpreadStreamMock).toHaveBeenCalled());

        act(() => { openSpreadStreamMock.mock.calls[0][1].onRejected?.(403); });

        expect(result.current.isLoading).toBe(false);
        expect(navigateMock).not.toHaveBeenCalled();
    });

    it('handleSpreadClosedConfirm closes the dialog and navigates to /spreads', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { result } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        act(() => { result.current.handleSpreadClosedConfirm(); });

        expect(result.current.isSpreadClosedDialogOpen).toBe(false);
        expect(navigateMock).toHaveBeenCalledWith('/en/spreads', undefined);
    });

    it('closes the stream on unmount', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { unmount } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(openSpreadStreamMock).toHaveBeenCalled());
        expect(streamHandle.close).not.toHaveBeenCalled();

        unmount();

        expect(streamHandle.close).toHaveBeenCalledTimes(1);
    });

    it('opens a single stream across re-renders of the same spread', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { result, rerender } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        rerender();
        rerender();

        expect(openSpreadStreamMock).toHaveBeenCalledTimes(1);
        expect(streamHandle.close).not.toHaveBeenCalled();
    });
});
