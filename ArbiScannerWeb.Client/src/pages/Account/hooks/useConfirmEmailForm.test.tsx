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
    return { ...actual, useNavigate: () => navigateMock, useSearchParams: () => [searchParams, vi.fn()] };
});

async function buildWrapper() {
    const { accountApi } = await import('../../../store/services/account');
    const store = configureStore({
        reducer: { account: accountReducer, [accountApi.reducerPath]: accountApi.reducer },
        middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(accountApi.middleware),
        preloadedState: {
            account: {
                account: createEmptyAccountModel(), isLoggedIn: false, loading: false, error: null,
                emailConfirmToken: null, needsEmailConfirmation: false, sessionChecked: true,
            },
        },
    });
    return ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>;
}

describe('useConfirmEmailForm', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        navigateMock.mockClear();
        searchParams = new URLSearchParams();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
    });

    it('picks up the token from the URL query string', async () => {
        searchParams = new URLSearchParams('emailConfirmToken=tok-1');
        const wrapper = await buildWrapper();
        const { useConfirmEmailForm } = await import('./useConfirmEmailForm');

        const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });

        expect(result.current.errors).toEqual({ token: '', server: '' });

        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
        Object.defineProperty(result.current.tokenRef, 'current', { value: { value: '123456' }, writable: true });
        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(rawBaseQueryMock.mock.calls[0][0]).toMatchObject({
            url: '/Account/ConfirmEmail',
            body: { emailConfirmToken: 'tok-1', token: '123456' },
        });
    });

    it('requires a token before submitting', async () => {
        const wrapper = await buildWrapper();
        const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
        const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.token).not.toBe('');
        expect(rawBaseQueryMock).not.toHaveBeenCalled();
    });

    it('clears the session and navigates to login on success', async () => {
        searchParams = new URLSearchParams('emailConfirmToken=tok-1');
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
        const wrapper = await buildWrapper();
        const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
        const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });
        Object.defineProperty(result.current.tokenRef, 'current', { value: { value: '123456' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(navigateMock).toHaveBeenCalledWith('/en/account/login?redirect=/account', undefined);
        expect(result.current.loading).toBe(false);
    });

    it('shows a server error when confirmation fails', async () => {
        searchParams = new URLSearchParams('emailConfirmToken=tok-1');
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, isFailed: true, errors: [{ message: 'Bad code' }] } });
        const wrapper = await buildWrapper();
        const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
        const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });
        Object.defineProperty(result.current.tokenRef, 'current', { value: { value: '000000' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.server).toBe('Bad code');
    });

    it('shows a network error message when the request throws', async () => {
        searchParams = new URLSearchParams('emailConfirmToken=tok-1');
        rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'down' } });
        const wrapper = await buildWrapper();
        const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
        const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });
        Object.defineProperty(result.current.tokenRef, 'current', { value: { value: '000000' }, writable: true });

        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });

        expect(result.current.errors.server).not.toBe('');
    });

    it('handleTokenInput clears both error fields', async () => {
        const wrapper = await buildWrapper();
        const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
        const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });
        await act(async () => {
            await result.current.handleSubmit({ preventDefault: vi.fn() } as never);
        });
        expect(result.current.errors.token).not.toBe('');

        act(() => { result.current.handleTokenInput(); });

        expect(result.current.errors).toEqual({ token: '', server: '' });
    });

    describe('handleResendCode', () => {
        it('requires a token', async () => {
            const wrapper = await buildWrapper();
            const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
            const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });

            await act(async () => { await result.current.handleResendCode(); });

            expect(result.current.errors.server).not.toBe('');
            expect(rawBaseQueryMock).not.toHaveBeenCalled();
        });

        it('resends successfully', async () => {
            searchParams = new URLSearchParams('emailConfirmToken=tok-1');
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true } });
            const wrapper = await buildWrapper();
            const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
            const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });

            await act(async () => { await result.current.handleResendCode(); });

            expect(rawBaseQueryMock.mock.calls[0][0]).toMatchObject({ url: '/Account/ResendEmailConfirmation' });
            expect(result.current.resending).toBe(false);
        });

        it('shows a server error when resend fails', async () => {
            searchParams = new URLSearchParams('emailConfirmToken=tok-1');
            rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: false, isFailed: true, reasons: [{ message: 'Too many attempts' }] } });
            const wrapper = await buildWrapper();
            const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
            const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });

            await act(async () => { await result.current.handleResendCode(); });

            expect(result.current.errors.server).toBe('Too many attempts');
        });

        it('shows a network error message when resend throws', async () => {
            searchParams = new URLSearchParams('emailConfirmToken=tok-1');
            rawBaseQueryMock.mockResolvedValue({ error: { status: 'FETCH_ERROR', error: 'down' } });
            const wrapper = await buildWrapper();
            const { useConfirmEmailForm } = await import('./useConfirmEmailForm');
            const { result } = renderHook(() => useConfirmEmailForm(), { wrapper });

            await act(async () => { await result.current.handleResendCode(); });

            expect(result.current.errors.server).not.toBe('');
        });
    });
});
