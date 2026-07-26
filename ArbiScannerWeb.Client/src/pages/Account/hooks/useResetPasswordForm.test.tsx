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

const STRONG_PASSWORD = 'Str0ng!Passw0rd';

describe('useResetPasswordForm', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
        window.history.pushState({}, '', '/en/account/resetpassword');
    });

    it('redirects home when already logged in', async () => {
        const wrapper = await buildWrapper(true);
        const { useResetPasswordForm } = await import('./useResetPasswordForm');

        renderHook(() => useResetPasswordForm(), { wrapper });

        expect(navigateMock).toHaveBeenCalledWith('/en/', undefined);
    });

    it('requires a token from the URL before submitting', async () => {
        const wrapper = await buildWrapper();
        const { useResetPasswordForm } = await import('./useResetPasswordForm');
        const { result } = renderHook(() => useResetPasswordForm(), { wrapper });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });
        Object.defineProperty(result.current.confirmPasswordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.server).not.toBe('');
        expect(rawBaseQueryMock).not.toHaveBeenCalled();
    });

    it('rejects a weak password', async () => {
        window.history.pushState({}, '', '/en/account/resetpassword?token=tok-1');
        const wrapper = await buildWrapper();
        const { useResetPasswordForm } = await import('./useResetPasswordForm');
        const { result } = renderHook(() => useResetPasswordForm(), { wrapper });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: 'weak' }, writable: true });
        Object.defineProperty(result.current.confirmPasswordRef, 'current', { value: { value: 'weak' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.password).not.toBe('');
        expect(rawBaseQueryMock).not.toHaveBeenCalled();
    });

    it('rejects mismatched passwords', async () => {
        window.history.pushState({}, '', '/en/account/resetpassword?token=tok-1');
        const wrapper = await buildWrapper();
        const { useResetPasswordForm } = await import('./useResetPasswordForm');
        const { result } = renderHook(() => useResetPasswordForm(), { wrapper });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });
        Object.defineProperty(result.current.confirmPasswordRef, 'current', { value: { value: 'Different!Pass1' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.password).not.toBe('');
        expect(rawBaseQueryMock).not.toHaveBeenCalled();
    });

    it('resets successfully and navigates to login', async () => {
        window.history.pushState({}, '', '/en/account/resetpassword?token=tok-1');
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
        const wrapper = await buildWrapper();
        const { useResetPasswordForm } = await import('./useResetPasswordForm');
        const { result } = renderHook(() => useResetPasswordForm(), { wrapper });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });
        Object.defineProperty(result.current.confirmPasswordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(rawBaseQueryMock.mock.calls[0][0]).toMatchObject({
            url: '/Account/ResetPassword',
            body: { token: 'tok-1', newPassword: STRONG_PASSWORD },
        });
        expect(navigateMock).toHaveBeenCalledWith('/en/account/login', undefined);
    });

    it('shows a server error when the API reports failure', async () => {
        window.history.pushState({}, '', '/en/account/resetpassword?token=tok-1');
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, isFailed: true, errors: [{ message: 'Token expired' }] } });
        const wrapper = await buildWrapper();
        const { useResetPasswordForm } = await import('./useResetPasswordForm');
        const { result } = renderHook(() => useResetPasswordForm(), { wrapper });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });
        Object.defineProperty(result.current.confirmPasswordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.server).toBe('Token expired');
    });

    it('shows a network error message when the request throws', async () => {
        window.history.pushState({}, '', '/en/account/resetpassword?token=tok-1');
        rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'down' } });
        const wrapper = await buildWrapper();
        const { useResetPasswordForm } = await import('./useResetPasswordForm');
        const { result } = renderHook(() => useResetPasswordForm(), { wrapper });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });
        Object.defineProperty(result.current.confirmPasswordRef, 'current', { value: { value: STRONG_PASSWORD }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.server).not.toBe('');
    });

    it('handlePasswordInput clears the password error', async () => {
        const wrapper = await buildWrapper();
        const { useResetPasswordForm } = await import('./useResetPasswordForm');
        const { result } = renderHook(() => useResetPasswordForm(), { wrapper });
        Object.defineProperty(result.current.passwordRef, 'current', { value: { value: 'weak' }, writable: true });
        Object.defineProperty(result.current.confirmPasswordRef, 'current', { value: { value: 'weak' }, writable: true });
        window.history.pushState({}, '', '/en/account/resetpassword?token=tok-1');
        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        act(() => { result.current.handlePasswordInput(); });

        expect(result.current.errors.password).toBe('');
    });
});
