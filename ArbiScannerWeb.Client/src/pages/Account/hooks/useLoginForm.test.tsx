import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import type { ReactNode } from 'react';
import accountReducer from '../../../store/slices/accountSlice';
import { createEmptyAccountModel } from '../../../types/accountType';
import { createLocalStorageMock } from '../../../test/localStorageMock';

const rawBaseQueryMock = vi.fn();
const navigateMock = vi.fn();
let searchParams = new URLSearchParams();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return {
        ...actual,
        useNavigate: () => navigateMock,
        useSearchParams: () => [searchParams, vi.fn()],
    };
});

async function buildWrapper(isLoggedIn = false, extra: Partial<Record<string, unknown>> = {}) {
    const { accountApi } = await import('../../../store/services/account');
    const store = configureStore({
        reducer: { account: accountReducer, [accountApi.reducerPath]: accountApi.reducer },
        middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(accountApi.middleware),
        preloadedState: {
            account: {
                account: createEmptyAccountModel(),
                isLoggedIn,
                loading: false,
                error: null,
                emailConfirmToken: null,
                needsEmailConfirmation: false,
                sessionChecked: true,
                ...extra,
            },
        },
    });
    return {
        store,
        wrapper: ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>,
    };
}

describe('useLoginForm', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        searchParams = new URLSearchParams();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('clears any prior error on mount', async () => {
        const { wrapper, store } = await buildWrapper(false, { error: 'stale error' });
        const { useLoginForm } = await import('./useLoginForm');

        renderHook(() => useLoginForm(), { wrapper });

        expect(store.getState().account.error).toBeNull();
    });

    it('redirects to /spreads when already logged in', async () => {
        const { wrapper } = await buildWrapper(true);
        const { useLoginForm } = await import('./useLoginForm');

        renderHook(() => useLoginForm(), { wrapper });

        expect(navigateMock).toHaveBeenCalledWith('/en/spreads', undefined);
    });

    it('redirects to a relative redirect query param when logged in', async () => {
        searchParams = new URLSearchParams('redirect=/subscription');
        const { wrapper } = await buildWrapper(true);
        const { useLoginForm } = await import('./useLoginForm');

        renderHook(() => useLoginForm(), { wrapper });

        expect(navigateMock).toHaveBeenCalledWith('/en/subscription', undefined);
    });

    it('ignores an absolute-URL redirect query param', async () => {
        searchParams = new URLSearchParams('redirect=https://evil.example.com');
        const { wrapper } = await buildWrapper(true);
        const { useLoginForm } = await import('./useLoginForm');

        renderHook(() => useLoginForm(), { wrapper });

        expect(navigateMock).toHaveBeenCalledWith('/en/spreads', undefined);
    });

    it('navigates to confirm-email when the account needs confirmation', async () => {
        const { wrapper } = await buildWrapper(false, { needsEmailConfirmation: true, emailConfirmToken: 'tok' });
        const { useLoginForm } = await import('./useLoginForm');

        renderHook(() => useLoginForm(), { wrapper });

        expect(navigateMock).toHaveBeenCalledWith('/en/account/confirmemail?emailConfirmToken=tok', undefined);
    });

    it('rejects an invalid email without calling the login mutation', async () => {
        const { wrapper } = await buildWrapper();
        const { useLoginForm } = await import('./useLoginForm');
        const { result } = renderHook(() => useLoginForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: 'not-an-email' }, writable: true });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: 'longenoughpassword' }, writable: true });

        act(() => {
            result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.email).not.toBe('');
        expect(rawBaseQueryMock).not.toHaveBeenCalled();
    });

    it('rejects a too-short password without calling the login mutation', async () => {
        const { wrapper } = await buildWrapper();
        const { useLoginForm } = await import('./useLoginForm');
        const { result } = renderHook(() => useLoginForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: 'a@b.com' }, writable: true });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: 'short' }, writable: true });

        act(() => {
            result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.password).not.toBe('');
        expect(rawBaseQueryMock).not.toHaveBeenCalled();
    });

    it('submits valid credentials and toggles loading', async () => {
        let resolveQuery!: (value: unknown) => void;
        rawBaseQueryMock.mockReturnValue(new Promise((resolve) => { resolveQuery = resolve; }));
        const { wrapper } = await buildWrapper();
        const { useLoginForm } = await import('./useLoginForm');
        const { result } = renderHook(() => useLoginForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: 'a@b.com' }, writable: true });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: 'longenoughpassword' }, writable: true });

        act(() => {
            result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.loading).toBe(true);

        await act(async () => {
            resolveQuery({ data: { isSuccess: false, errors: [{ message: 'nope' }] } });
            await Promise.resolve();
        });

        expect(result.current.loading).toBe(false);
    });

    it('clearFieldError resets a single field', async () => {
        const { wrapper } = await buildWrapper();
        const { useLoginForm } = await import('./useLoginForm');
        const { result } = renderHook(() => useLoginForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: '' }, writable: true });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: '' }, writable: true });
        act(() => {
            result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });
        expect(result.current.errors.email).not.toBe('');

        act(() => {
            result.current.clearFieldError('email');
        });

        expect(result.current.errors.email).toBe('');
    });
});
