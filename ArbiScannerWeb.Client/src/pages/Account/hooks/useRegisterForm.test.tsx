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
                account: createEmptyAccountModel(),
                isLoggedIn,
                loading: false,
                error: null,
                emailConfirmToken: null,
                needsEmailConfirmation: false,
                sessionChecked: true,
            },
        },
    });
    return ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>;
}

function fillRefs(result: { emailRef: React.RefObject<HTMLInputElement | null>; confirmEmailRef: React.RefObject<HTMLInputElement | null>; passwordRef: React.RefObject<HTMLInputElement | null>; confirmPasswordRef: React.RefObject<HTMLInputElement | null>; }, email: string, confirmEmail: string, password: string, confirmPassword: string) {
    Object.defineProperty(result.emailRef, 'current', { value: { value: email }, writable: true });
    Object.defineProperty(result.confirmEmailRef, 'current', { value: { value: confirmEmail }, writable: true });
    Object.defineProperty(result.passwordRef, 'current', { value: { value: password }, writable: true });
    Object.defineProperty(result.confirmPasswordRef, 'current', { value: { value: confirmPassword }, writable: true });
}

describe('useRegisterForm', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('redirects to /spreads when already logged in', async () => {
        const wrapper = await buildWrapper(true);
        const { useRegisterForm } = await import('./useRegisterForm');

        renderHook(() => useRegisterForm(), { wrapper });

        expect(navigateMock).toHaveBeenCalledWith('/en/spreads', undefined);
    });

    it('shows the EULA modal only when validation passes', async () => {
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, 'a@b.com', 'a@b.com', 'Str0ng!Passw0rd', 'Str0ng!Passw0rd');

        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });

        expect(result.current.showEulaModal).toBe(true);
        expect(result.current.errors.email).toBe('');
    });

    it('rejects mismatched emails without showing the EULA modal', async () => {
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, 'a@b.com', 'different@b.com', 'Str0ng!Passw0rd', 'Str0ng!Passw0rd');

        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });

        expect(result.current.showEulaModal).toBe(false);
        expect(result.current.errors.confirmEmail).not.toBe('');
    });

    it('rejects mismatched passwords', async () => {
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, 'a@b.com', 'a@b.com', 'Str0ng!Passw0rd', 'Different!Pass1');

        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });

        expect(result.current.errors.confirmPassword).not.toBe('');
    });

    it('rejects a weak password', async () => {
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, 'a@b.com', 'a@b.com', 'weak', 'weak');

        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });

        expect(result.current.errors.password).not.toBe('');
    });

    it('registers successfully and navigates to confirm-email on EULA agreement', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: { id: 'code-1' } } });
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, 'a@b.com', 'a@b.com', 'Str0ng!Passw0rd', 'Str0ng!Passw0rd');
        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });

        await act(async () => { await result.current.handleEulaAgree(); });

        expect(navigateMock).toHaveBeenCalledWith('/en/account/confirmemail?emailConfirmToken=code-1', undefined);
        expect(result.current.loading).toBe(false);
    });

    it('shows a server error when registration fails', async () => {
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, isFailed: true, errors: [{ message: 'Email taken' }] } });
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, 'a@b.com', 'a@b.com', 'Str0ng!Passw0rd', 'Str0ng!Passw0rd');
        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });

        await act(async () => { await result.current.handleEulaAgree(); });

        expect(result.current.errors.server).toBe('Email taken');
    });

    it('shows a normalized error message on a network failure', async () => {
        rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'down' } });
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, 'a@b.com', 'a@b.com', 'Str0ng!Passw0rd', 'Str0ng!Passw0rd');
        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });

        await act(async () => { await result.current.handleEulaAgree(); });

        expect(result.current.errors.server).toContain('Unable to reach the server');
    });

    it('handleEulaCancel hides the modal', async () => {
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, 'a@b.com', 'a@b.com', 'Str0ng!Passw0rd', 'Str0ng!Passw0rd');
        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });
        expect(result.current.showEulaModal).toBe(true);

        act(() => { result.current.handleEulaCancel(); });

        expect(result.current.showEulaModal).toBe(false);
    });

    it('clearFieldError resets a single field', async () => {
        const wrapper = await buildWrapper();
        const { useRegisterForm } = await import('./useRegisterForm');
        const { result } = renderHook(() => useRegisterForm(), { wrapper });
        fillRefs(result.current, '', '', '', '');
        act(() => { result.current.handleSubmit({ preventDefault: vi.fn() } as never); });
        expect(result.current.errors.email).not.toBe('');

        act(() => { result.current.clearFieldError('email'); });

        expect(result.current.errors.email).toBe('');
    });
});
