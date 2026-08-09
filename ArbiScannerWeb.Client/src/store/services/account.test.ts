import { beforeEach, describe, expect, it, vi } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';
import accountReducer from '../slices/accountSlice';
import { createEmptyAccountModel } from '../../types/accountType';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

async function buildStore() {
    const { accountApi } = await import('./account');
    return {
        accountApi,
        store: configureStore({
            reducer: { account: accountReducer, [accountApi.reducerPath]: accountApi.reducer },
            middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(accountApi.middleware),
        }),
    };
}

describe('accountApi', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
    });

    describe('getUserData', () => {
        it('authenticates the account on success', async () => {
            const account = { ...createEmptyAccountModel(), id: 'u1' };
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: account } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.getUserData.initiate());

            expect(store.getState().account.isLoggedIn).toBe(true);
        });

        it('logs out and sets an error when the API reports failure', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, reasons: [{ message: 'Session expired' }] } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.getUserData.initiate());

            expect(store.getState().account.isLoggedIn).toBe(false);
            expect(store.getState().account.error).toBe('Session expired');
        });

        it('logs out when the request itself errors out', async () => {
            rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'Failed to fetch' } });
            const { accountApi, store } = await buildStore();
            store.dispatch({ type: 'account/setAuthenticatedAccount', payload: createEmptyAccountModel() });

            await store.dispatch(accountApi.endpoints.getUserData.initiate());

            expect(store.getState().account.isLoggedIn).toBe(false);
        });
    });

    describe('updateAccountDetails', () => {
        const payload = {
            userName: 'me', spreadSize: 1, positionSize: 1, futuresSpread: true,
            fundingSpread: false, spotSpread: false, haveAccess: true, email: 'a@b.com', exchanges: [],
        };

        it('clears the error without touching loading on success', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: payload } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.updateAccountDetails.initiate(payload));

            expect(store.getState().account.error).toBeNull();
        });

        it('sets an error and stops loading when the API reports failure', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, errors: [{ message: 'Invalid settings' }] } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.updateAccountDetails.initiate(payload));

            expect(store.getState().account.loading).toBe(false);
            expect(store.getState().account.error).toBe('Invalid settings');
        });

        it('sets an error when the request itself errors out', async () => {
            rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'Failed to fetch' } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.updateAccountDetails.initiate(payload));

            expect(store.getState().account.loading).toBe(false);
            expect(store.getState().account.error).toBeTruthy();
        });
    });

    describe('plain endpoints without onQueryStarted', () => {
        it('createTelegramLinkRequest posts to the expected URL', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.createTelegramLinkRequest.initiate());

            expect(rawBaseQueryMock.mock.calls[0][0].url).toBe('/TelegramLink/CreateTelegramLinkRequest');
        });

        it('removeTelegramLink posts to the expected URL', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.removeTelegramLink.initiate());

            expect(rawBaseQueryMock.mock.calls[0][0].url).toBe('/TelegramLink/RemoveTelegramLink');
        });
    });
});
