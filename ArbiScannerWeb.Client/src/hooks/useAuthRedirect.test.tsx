import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import type { ReactNode } from 'react';
import accountReducer from '../store/slices/accountSlice';
import { createEmptyAccountModel } from '../types/accountType';
import { useAuthRedirect } from './useAuthRedirect';

const navigateMock = vi.fn();

vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock };
});

function buildStore(isLoggedIn: boolean) {
    return configureStore({
        reducer: { account: accountReducer },
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
}

function wrapper(store: ReturnType<typeof buildStore>) {
    return ({ children }: { children: ReactNode }) => <Provider store={store}>{children}</Provider>;
}

describe('useAuthRedirect', () => {
    beforeEach(() => {
        navigateMock.mockClear();
    });

    it('redirects to the default login path when not logged in', () => {
        renderHook(() => useAuthRedirect(), { wrapper: wrapper(buildStore(false)) });

        expect(navigateMock).toHaveBeenCalledWith('/account/login');
    });

    it('redirects to a custom path when provided', () => {
        renderHook(() => useAuthRedirect('/custom/login'), { wrapper: wrapper(buildStore(false)) });

        expect(navigateMock).toHaveBeenCalledWith('/custom/login');
    });

    it('does not redirect when already logged in', () => {
        renderHook(() => useAuthRedirect(), { wrapper: wrapper(buildStore(true)) });

        expect(navigateMock).not.toHaveBeenCalled();
    });
});
