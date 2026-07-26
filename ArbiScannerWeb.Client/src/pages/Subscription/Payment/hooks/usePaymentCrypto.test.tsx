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

interface RawQueryArgs { url: string; method?: string }

describe('usePaymentCrypto', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        searchParams = new URLSearchParams('id=5');
        rawBaseQueryMock.mockImplementation((args: RawQueryArgs) => {
            if (args.url.startsWith('/Subscription/GetSubscriptionDetails')) {
                return Promise.resolve({ data: { isSuccess: true, value: { id: 5, type: 'Pro', price: 9.99 } } });
            }
            if (args.url.startsWith('/Subscription/GetUserActivePayments')) {
                return Promise.resolve({ data: { isSuccess: true, value: { id: 42, payment: { status: 0 } } } });
            }
            if (args.url.startsWith('/Subscription/GetPaymentStatus')) {
                return Promise.resolve({ data: { isSuccess: true, value: { id: 42, payment: { status: 0, paymentUrl: 'https://pay.example/x' } } } });
            }
            if (args.url.startsWith('/Subscription/CancelPayment')) {
                return Promise.resolve({ data: { isSuccess: true } });
            }
            return Promise.resolve({ data: { isSuccess: true } });
        });
    });

    it('exposes subscription details once loaded', async () => {
        const wrapper = await buildWrapper();
        const { usePaymentCrypto } = await import('./usePaymentCrypto');

        const { result } = renderHook(() => usePaymentCrypto(), { wrapper });

        await waitFor(() => expect(result.current.subscriptionDetails).toEqual({ id: 5, type: 'Pro', price: 9.99 }));
        expect(result.current.isLoading).toBe(false);
    });

    it('exposes the payment URL once the payment status query resolves', async () => {
        const wrapper = await buildWrapper();
        const { usePaymentCrypto } = await import('./usePaymentCrypto');

        const { result } = renderHook(() => usePaymentCrypto(), { wrapper });

        await waitFor(() => expect(result.current.paymentUrl).toBe('https://pay.example/x'));
    });

    it('shows the thank-you modal once the payment completes', async () => {
        rawBaseQueryMock.mockImplementation((args: RawQueryArgs) => {
            if (args.url.startsWith('/Subscription/GetUserActivePayments')) {
                return Promise.resolve({ data: { isSuccess: true, value: { id: 42 } } });
            }
            if (args.url.startsWith('/Subscription/GetPaymentStatus')) {
                return Promise.resolve({ data: { isSuccess: true, value: { id: 42, payment: { status: 1 } } } });
            }
            return Promise.resolve({ data: { isSuccess: true } });
        });
        const wrapper = await buildWrapper();
        const { usePaymentCrypto } = await import('./usePaymentCrypto');

        const { result } = renderHook(() => usePaymentCrypto(), { wrapper });

        await waitFor(() => expect(result.current.showThankYouModal).toBe(true));
    });

    it('handleCloseThankYouModal hides the modal and navigates to /account', async () => {
        const wrapper = await buildWrapper();
        const { usePaymentCrypto } = await import('./usePaymentCrypto');
        const { result } = renderHook(() => usePaymentCrypto(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        act(() => { result.current.handleCloseThankYouModal(); });

        expect(result.current.showThankYouModal).toBe(false);
        expect(navigateMock).toHaveBeenCalledWith('/en/account', undefined);
    });

    it('handleCancel navigates back on success', async () => {
        const wrapper = await buildWrapper();
        const { usePaymentCrypto } = await import('./usePaymentCrypto');
        const { result } = renderHook(() => usePaymentCrypto(), { wrapper });
        await waitFor(() => expect(result.current.paymentUrl).toBeTruthy());

        await act(async () => { await result.current.handleCancel(); });

        expect(navigateMock).toHaveBeenCalledWith(-1);
    });

    it('handleCancel does not navigate when cancellation fails', async () => {
        rawBaseQueryMock.mockImplementation((args: RawQueryArgs) => {
            if (args.url.startsWith('/Subscription/CancelPayment')) {
                return Promise.resolve({ error: { status: 'FETCH_ERROR', error: 'down' } });
            }
            if (args.url.startsWith('/Subscription/GetUserActivePayments')) {
                return Promise.resolve({ data: { isSuccess: true, value: { id: 42 } } });
            }
            if (args.url.startsWith('/Subscription/GetPaymentStatus')) {
                return Promise.resolve({ data: { isSuccess: true, value: { id: 42, payment: { status: 0 } } } });
            }
            return Promise.resolve({ data: { isSuccess: true } });
        });
        const wrapper = await buildWrapper();
        const { usePaymentCrypto } = await import('./usePaymentCrypto');
        const { result } = renderHook(() => usePaymentCrypto(), { wrapper });
        await waitFor(() => expect(result.current.isLoading).toBe(false));

        await act(async () => { await result.current.handleCancel(); });

        expect(navigateMock).not.toHaveBeenCalledWith(-1);
    });
});
