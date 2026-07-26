import { beforeEach, describe, expect, it, vi } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

async function buildStore() {
    const { subscriptionsAPI } = await import('./subscription');
    return {
        subscriptionsAPI,
        store: configureStore({
            reducer: { [subscriptionsAPI.reducerPath]: subscriptionsAPI.reducer },
            middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(subscriptionsAPI.middleware),
        }),
    };
}

describe('subscriptionsAPI', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
    });

    it('getAllSubscriptions requests the expected URL', async () => {
        const { subscriptionsAPI, store } = await buildStore();

        await store.dispatch(subscriptionsAPI.endpoints.getAllSubscriptions.initiate());

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/Subscription/GetAllSubscriptions', method: 'GET' });
    });

    it('getSubscriptionDetails requests the expected URL with the subscription id', async () => {
        const { subscriptionsAPI, store } = await buildStore();

        await store.dispatch(subscriptionsAPI.endpoints.getSubscriptionDetails.initiate(7));

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({
            url: '/Subscription/GetSubscriptionDetails?subscriptionId=7',
            method: 'GET',
        });
    });

    it('getPaymentStatus requests the expected URL with an encoded payment id', async () => {
        const { subscriptionsAPI, store } = await buildStore();

        await store.dispatch(subscriptionsAPI.endpoints.getPaymentStatus.initiate(9));

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({
            url: '/Subscription/GetPaymentStatus?userSubscriptionPaymentId=9',
            method: 'GET',
        });
    });

    it('createPayment posts with the encoded subscription id', async () => {
        const { subscriptionsAPI, store } = await buildStore();

        await store.dispatch(subscriptionsAPI.endpoints.createPayment.initiate(3));

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({
            url: '/Subscription/CreatePayment?subscriptionId=3',
            method: 'POST',
        });
    });

    it('cancelPayment posts with the encoded payment id', async () => {
        const { subscriptionsAPI, store } = await buildStore();

        await store.dispatch(subscriptionsAPI.endpoints.cancelPayment.initiate(4));

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({
            url: '/Subscription/CancelPayment?userSubscriptionPaymentId=4',
            method: 'POST',
        });
    });

    it('getUserActiveSubscriptions requests the expected URL', async () => {
        const { subscriptionsAPI, store } = await buildStore();

        await store.dispatch(subscriptionsAPI.endpoints.getUserActiveSubscriptions.initiate());

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/Subscription/GetUserActiveSubscriptions', method: 'GET' });
    });

    it('getUserActivePayments requests the expected URL', async () => {
        const { subscriptionsAPI, store } = await buildStore();

        await store.dispatch(subscriptionsAPI.endpoints.getUserActivePayments.initiate());

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/Subscription/GetUserActivePayments', method: 'GET' });
    });
});
