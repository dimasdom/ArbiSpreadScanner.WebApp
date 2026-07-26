import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import type { ReactNode } from 'react';

const rawBaseQueryMock = vi.fn();
const navigateMock = vi.fn();
let searchParams = new URLSearchParams('id=5');

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock, useSearchParams: () => [searchParams, vi.fn()] };
});

async function buildWrapper() {
    const { subscriptionsAPI } = await import('../../../../store/services/subscription');
    const store = configureStore({
        reducer: { [subscriptionsAPI.reducerPath]: subscriptionsAPI.reducer },
        middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(subscriptionsAPI.middleware),
    });
    return ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>;
}

describe('usePaymentInfo', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        searchParams = new URLSearchParams('id=5');
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: { id: 5, type: 'Pro', price: 9.99 } } });
    });

    it('exposes subscription details once loaded', async () => {
        const wrapper = await buildWrapper();
        const { usePaymentInfo } = await import('./usePaymentInfo');

        const { result } = renderHook(() => usePaymentInfo(), { wrapper });

        await waitFor(() => expect(result.current.subscriptionDetails).toEqual({ id: 5, type: 'Pro', price: 9.99 }));
        expect(result.current.isLoading).toBe(false);
    });

    it('creates a payment then navigates to the payment page on success', async () => {
        const wrapper = await buildWrapper();
        const { usePaymentInfo } = await import('./usePaymentInfo');
        const { result } = renderHook(() => usePaymentInfo(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        await act(async () => { await result.current.handlePayClick(); });

        expect(rawBaseQueryMock.mock.calls.some((c) => c[0].url === '/Subscription/CreatePayment?subscriptionId=5')).toBe(true);
        expect(navigateMock).toHaveBeenCalledWith('/en/payment/pay?id=5', undefined);
    });

    it('still navigates to the payment page when payment creation fails', async () => {
        rawBaseQueryMock.mockImplementation((args: { url: string }) => {
            if (args.url.startsWith('/Subscription/CreatePayment')) {
                return Promise.resolve({ error: { status: 'FETCH_ERROR', error: 'down' } });
            }
            return Promise.resolve({ data: { isSuccess: true, value: { id: 5 } } });
        });
        const wrapper = await buildWrapper();
        const { usePaymentInfo } = await import('./usePaymentInfo');
        const { result } = renderHook(() => usePaymentInfo(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        await act(async () => { await result.current.handlePayClick(); });

        expect(navigateMock).toHaveBeenCalledWith('/en/payment/pay?id=5', undefined);
    });
});
