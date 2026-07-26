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

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock };
});

async function buildWrapper(isLoggedIn = false) {
    const { accountApi } = await import('../../../store/services/account');
    const store = configureStore({
        reducer: { account: accountReducer, [accountApi.reducerPath]: accountApi.reducer },
        middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(accountApi.middleware),
        preloadedState: {
            account: {
                account: createEmptyAccountModel(), isLoggedIn, loading: false, error: null,
                emailConfirmToken: null, needsEmailConfirmation: false, sessionChecked: true,
            },
        },
    });
    return ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>;
}

describe('useForgotPasswordForm', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('redirects home when already logged in', async () => {
        const wrapper = await buildWrapper(true);
        const { useForgotPasswordForm } = await import('./useForgotPasswordForm');

        renderHook(() => useForgotPasswordForm(), { wrapper });

        expect(navigateMock).toHaveBeenCalledWith('/en/', undefined);
    });

    it('rejects an invalid email without calling the API', async () => {
        const wrapper = await buildWrapper();
        const { useForgotPasswordForm } = await import('./useForgotPasswordForm');
        const { result } = renderHook(() => useForgotPasswordForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: 'not-an-email' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.email).not.toBe('');
        expect(rawBaseQueryMock).not.toHaveBeenCalled();
    });

    it('marks success and records the submitted email', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
        const wrapper = await buildWrapper();
        const { useForgotPasswordForm } = await import('./useForgotPasswordForm');
        const { result } = renderHook(() => useForgotPasswordForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: 'a@b.com' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.isSuccess).toBe(true);
        expect(result.current.submittedEmail).toBe('a@b.com');
        expect(result.current.loading).toBe(false);
    });

    it('shows a server error when the API reports failure', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, isFailed: true, errors: [{ message: 'No such user' }] } });
        const wrapper = await buildWrapper();
        const { useForgotPasswordForm } = await import('./useForgotPasswordForm');
        const { result } = renderHook(() => useForgotPasswordForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: 'a@b.com' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.server).toBe('No such user');
        expect(result.current.isSuccess).toBe(false);
    });

    it('shows a network error message when the request throws', async () => {
        rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'down' } });
        const wrapper = await buildWrapper();
        const { useForgotPasswordForm } = await import('./useForgotPasswordForm');
        const { result } = renderHook(() => useForgotPasswordForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: 'a@b.com' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.server).not.toBe('');
        expect(result.current.loading).toBe(false);
    });

    it('handleEmailInput clears the email error', async () => {
        const wrapper = await buildWrapper();
        const { useForgotPasswordForm } = await import('./useForgotPasswordForm');
        const { result } = renderHook(() => useForgotPasswordForm(), { wrapper });
        Object.defineProperty(result.current.emailRef, 'current', { value: { value: 'bad' }, writable: true });
        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });
        expect(result.current.errors.email).not.toBe('');

        act(() => { result.current.handleEmailInput(); });

        expect(result.current.errors.email).toBe('');
    });
});
