import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import type { ReactNode } from 'react';
import accountReducer from '../../../store/slices/accountSlice';
import { createEmptyAccountModel } from '../../../types/accountType';
import type { MessageDTO } from '../../../types/tickerType';
import { ChatContextProvider } from '../../../contexts/ChatContext';

const rawBaseQueryMock = vi.fn();
const navigateMock = vi.fn();
let searchParams = new URLSearchParams('id=abc-123');

const signalRServiceMock = {
    connect: vi.fn().mockResolvedValue(undefined),
    disconnect: vi.fn().mockResolvedValue(undefined),
    joinGroup: vi.fn().mockResolvedValue(undefined),
    leaveGroup: vi.fn().mockResolvedValue(undefined),
    onTickerUpdate: vi.fn(),
    offTickerUpdate: vi.fn(),
    isConnected: vi.fn().mockReturnValue(true),
};

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock, useSearchParams: () => [searchParams, vi.fn()] };
});

vi.mock('../../../services/signalrService', () => ({ signalRService: signalRServiceMock }));

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
        Object.values(signalRServiceMock).forEach((fn) => 'mockClear' in fn && fn.mockClear());
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: buildSpreadDetails() } });
    });

    it('navigates to /spreads when there is no spread id', async () => {
        searchParams = new URLSearchParams();
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/en/spreads', undefined));
    });

    it('loads the spread and connects to SignalR for live updates', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        const { result } = renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(result.current.isLoading).toBe(false));
        expect(result.current.pos?.guid).toBe('abc-123');
        await waitFor(() => expect(signalRServiceMock.joinGroup).toHaveBeenCalledWith('group-abc-123'));
    });

    it('navigates away when the spread query errors', async () => {
        rawBaseQueryMock.mockResolvedValue({ error: { status: 404, data: 'not found' } });
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');

        renderHook(() => useSpreadPage(), { wrapper });

        await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/en/spreads', undefined));
    });

    it('appends an incoming ticker update to the ticker list', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { result } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(signalRServiceMock.onTickerUpdate).toHaveBeenCalled());
        const handler = signalRServiceMock.onTickerUpdate.mock.calls[0][0] as (m: MessageDTO) => void;

        act(() => {
            handler({
                possiblePosition: { actionType: 1 } as never,
                ticker: { echangeA: 'binance', exchangeLong: 'okx', exchangeShort: 'binance', rateA: 100, rateB: 101, spread: 1 } as never,
            });
        });

        expect(result.current.tickers).toHaveLength(1);
    });

    it('opens the closed-dialog when a Close message arrives', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { result } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(signalRServiceMock.onTickerUpdate).toHaveBeenCalled());
        const handler = signalRServiceMock.onTickerUpdate.mock.calls[0][0] as (m: MessageDTO) => void;

        act(() => {
            handler({ possiblePosition: { actionType: 2 } as never, ticker: undefined as never });
        });

        expect(result.current.isSpreadClosedDialogOpen).toBe(true);
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

    it('disconnects from SignalR on unmount', async () => {
        const wrapper = await buildWrapper();
        const { useSpreadPage } = await import('./useSpreadPage');
        const { unmount } = renderHook(() => useSpreadPage(), { wrapper });
        await waitFor(() => expect(signalRServiceMock.joinGroup).toHaveBeenCalled());

        unmount();

        await waitFor(() => expect(signalRServiceMock.disconnect).toHaveBeenCalled());
        expect(signalRServiceMock.offTickerUpdate).toHaveBeenCalled();
    });
});
