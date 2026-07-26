import { beforeEach, describe, expect, it, vi } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';
import accountReducer from '../slices/accountSlice';
import { createEmptyAccountModel } from '../../types/accountType';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

const createLocalStorageMock = (): Storage => {
    const store = new Map<string, string>();
    return {
        getItem: (key: string) => store.get(key) ?? null,
        setItem: (key: string, value: string) => { store.set(key, value); },
        removeItem: (key: string) => { store.delete(key); },
        clear: () => { store.clear(); },
        key: (index: number) => Array.from(store.keys())[index] ?? null,
        get length() { return store.size; },
    } as Storage;
};

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
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    describe('login', () => {
        it('authenticates when the account has a confirmed email', async () => {
            const account = { ...createEmptyAccountModel(), id: 'u1', emailConfirmed: true };
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: account } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.login.initiate({ login: 'a@b.com', password: 'pw' }));

            expect(store.getState().account.isLoggedIn).toBe(true);
            expect(store.getState().account.account.id).toBe('u1');
        });

        it('requires email confirmation when the account is unconfirmed', async () => {
            const account = { ...createEmptyAccountModel(), emailConfirmed: false, emailConfirmToken: 'tok-123' };
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: account } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.login.initiate({ login: 'a@b.com', password: 'pw' }));

            expect(store.getState().account.needsEmailConfirmation).toBe(true);
            expect(store.getState().account.emailConfirmToken).toBe('tok-123');
            expect(store.getState().account.isLoggedIn).toBe(false);
        });

        it('sets an error message when the API reports failure', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, errors: [{ message: 'Invalid credentials' }] } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.login.initiate({ login: 'a@b.com', password: 'wrong' }));

            expect(store.getState().account.error).toBe('Invalid credentials');
            expect(store.getState().account.isLoggedIn).toBe(false);
        });

        it('falls back to a default error message when none is provided', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, errors: [] } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.login.initiate({ login: 'a@b.com', password: 'wrong' }));

            expect(store.getState().account.error).toBe('Login failed');
        });

        it('sets an error and stops loading when the request itself errors out', async () => {
            rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'Failed to fetch' } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.login.initiate({ login: 'a@b.com', password: 'pw' }));

            expect(store.getState().account.loading).toBe(false);
            expect(store.getState().account.error).toBeTruthy();
        });
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

    describe('logout', () => {
        it('clears the session once the request settles', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
            const { accountApi, store } = await buildStore();
            store.dispatch({ type: 'account/setAuthenticatedAccount', payload: createEmptyAccountModel() });

            await store.dispatch(accountApi.endpoints.logout.initiate());

            expect(store.getState().account.isLoggedIn).toBe(false);
        });

        it('still clears the session when the request fails', async () => {
            rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'Failed to fetch' } });
            const { accountApi, store } = await buildStore();
            store.dispatch({ type: 'account/setAuthenticatedAccount', payload: createEmptyAccountModel() });

            await store.dispatch(accountApi.endpoints.logout.initiate());

            expect(store.getState().account.isLoggedIn).toBe(false);
        });
    });

    describe('plain endpoints without onQueryStarted', () => {
        it.each([
            ['register', { login: 'a@b.com', password: 'pw' }, '/Account/Register', 'POST'],
            ['forgotPassword', { email: 'a@b.com' }, '/Account/ForgotPassword', 'POST'],
            ['resetPassword', { token: 't', newPassword: 'p' }, '/Account/ResetPassword', 'POST'],
            ['confirmEmail', { emailConfirmToken: 't', token: 'c' }, '/Account/ConfirmEmail', 'POST'],
            ['resendEmailRequest', { emailConfirmToken: 't' }, '/Account/ResendEmailConfirmation', 'POST'],
            ['changeEmailRequest', { email: 'new@b.com' }, '/Account/ChangeEmailRequest', 'POST'],
        ] as const)('%s calls %s with method %s', async (endpoint, arg, expectedUrl, expectedMethod) => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
            const { accountApi, store } = await buildStore();

            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            await store.dispatch((accountApi.endpoints as any)[endpoint].initiate(arg));

            const [requestArg] = rawBaseQueryMock.mock.calls[0];
            expect(requestArg.url).toBe(expectedUrl);
            expect(requestArg.method).toBe(expectedMethod);
        });

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

        it('getUserActiveSubscription gets the expected URL', async () => {
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
            const { accountApi, store } = await buildStore();

            await store.dispatch(accountApi.endpoints.getUserActiveSubscription.initiate());

            expect(rawBaseQueryMock.mock.calls[0][0].url).toBe('/Subscription/GetUserActiveSubscriptions');
        });
    });
});
