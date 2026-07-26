import { describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import type { ReactNode } from 'react';
import accountReducer from './store/slices/accountSlice';
import { createEmptyAccountModel } from './types/accountType';
import { useAppDispatch, useAppSelector } from './hooks';

function wrapper({ children }: { children: ReactNode }) {
    const store = configureStore({
        reducer: { account: accountReducer },
        preloadedState: {
            account: {
                account: createEmptyAccountModel(),
                isLoggedIn: true,
                loading: false,
                error: null,
                emailConfirmToken: null,
                needsEmailConfirmation: false,
                sessionChecked: true,
            },
        },
    });
    return <Provider store={store}>{children}</Provider>;
}

describe('typed store hooks', () => {
    it('useAppSelector reads typed state from the store', () => {
        const { result } = renderHook(() => useAppSelector((state) => state.account.isLoggedIn), { wrapper });

        expect(result.current).toBe(true);
    });

    it('useAppDispatch returns a callable dispatch function', () => {
        const { result } = renderHook(() => useAppDispatch(), { wrapper });

        expect(typeof result.current).toBe('function');
    });
});
