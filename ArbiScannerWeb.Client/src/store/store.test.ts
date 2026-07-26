import { beforeEach, describe, expect, it, vi } from 'vitest';

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

describe('root store', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: [] } });
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('combines all expected reducer slices', async () => {
        const { default: store } = await import('./store');
        const { accountApi } = await import('./services/account');
        const { spreadApi } = await import('./services/spread');
        const { subscriptionsAPI } = await import('./services/subscription');

        const state = store.getState();

        expect(state).toHaveProperty('account');
        expect(state).toHaveProperty('language');
        expect(state).toHaveProperty(accountApi.reducerPath);
        expect(state).toHaveProperty(spreadApi.reducerPath);
        expect(state).toHaveProperty(subscriptionsAPI.reducerPath);
    });

    it('resets subscriptionsAPI cache when the user logs out', async () => {
        const { default: store } = await import('./store');
        const { subscriptionsAPI } = await import('./services/subscription');
        const { logout } = await import('./slices/accountSlice');

        await store.dispatch(subscriptionsAPI.endpoints.getAllSubscriptions.initiate());
        expect(Object.keys(store.getState()[subscriptionsAPI.reducerPath].queries)).not.toHaveLength(0);

        store.dispatch(logout());

        expect(Object.keys(store.getState()[subscriptionsAPI.reducerPath].queries)).toHaveLength(0);
    });

    it('resets subscriptionsAPI cache when user data is cleared', async () => {
        const { default: store } = await import('./store');
        const { subscriptionsAPI } = await import('./services/subscription');
        const { clearUserData } = await import('./slices/accountSlice');

        await store.dispatch(subscriptionsAPI.endpoints.getAllSubscriptions.initiate());
        expect(Object.keys(store.getState()[subscriptionsAPI.reducerPath].queries)).not.toHaveLength(0);

        store.dispatch(clearUserData());

        expect(Object.keys(store.getState()[subscriptionsAPI.reducerPath].queries)).toHaveLength(0);
    });
});
