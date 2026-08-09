import { describe, it, expect } from 'vitest';
import accountReducer, {
    setAuthenticatedAccount,
    setLoading,
    setError,
    markSessionChecked,
    logout,
    clearError,
    type AccountState,
} from './accountSlice';
import { createEmptyAccountModel } from '../../types/accountType';
import type { AccountModel } from '../../types/accountType';

const emptyAccount = createEmptyAccountModel();

const initialState: AccountState = {
    account: emptyAccount,
    isLoggedIn: false,
    loading: false,
    error: null,
    sessionChecked: false,
};

const testAccount: AccountModel = {
    id: 'user-123',
    userName: 'testuser',
    email: 'test@example.com',
    emailConfirmed: true,
    telegramUserId: 0,
    userSettings: {
        id: 1,
        chatId: 0,
        spreadSize: 1,
        positionSize: 100,
        futuresSpread: true,
        fundingSpread: false,
        spotSpread: false,
        haveAccess: true,
        active: true,
        exchanges: [],
    },
};

describe('accountSlice reducer', () => {
    it('returns initial state for unknown action', () => {
        const state = accountReducer(undefined, { type: '@@INIT' });
        expect(state.isLoggedIn).toBe(false);
        expect(state.sessionChecked).toBe(false);
        expect(state.error).toBeNull();
    });

    describe('setAuthenticatedAccount', () => {
        it('sets account data and marks as logged in', () => {
            const state = accountReducer(initialState, setAuthenticatedAccount(testAccount));
            expect(state.isLoggedIn).toBe(true);
            expect(state.account).toEqual(testAccount);
            expect(state.sessionChecked).toBe(true);
        });

        it('clears error and loading on success', () => {
            const dirtyState: AccountState = { ...initialState, loading: true, error: 'old error' };
            const state = accountReducer(dirtyState, setAuthenticatedAccount(testAccount));
            expect(state.loading).toBe(false);
            expect(state.error).toBeNull();
        });
    });

    describe('setLoading', () => {
        it('sets loading true', () => {
            const state = accountReducer(initialState, setLoading(true));
            expect(state.loading).toBe(true);
        });

        it('sets loading false', () => {
            const loadingState = { ...initialState, loading: true };
            const state = accountReducer(loadingState, setLoading(false));
            expect(state.loading).toBe(false);
        });
    });

    describe('setError', () => {
        it('stores error message', () => {
            const state = accountReducer(initialState, setError('Something went wrong'));
            expect(state.error).toBe('Something went wrong');
        });

        it('clears error when set to null', () => {
            const errorState = { ...initialState, error: 'existing error' };
            const state = accountReducer(errorState, setError(null));
            expect(state.error).toBeNull();
        });
    });

    describe('markSessionChecked', () => {
        it('sets sessionChecked to true', () => {
            const state = accountReducer(initialState, markSessionChecked());
            expect(state.sessionChecked).toBe(true);
        });
    });

    describe('logout', () => {
        it('resets auth state and marks session as checked', () => {
            const loggedInState: AccountState = {
                ...initialState,
                account: testAccount,
                isLoggedIn: true,
                sessionChecked: true,
                error: 'some error',
            };
            const state = accountReducer(loggedInState, logout());
            expect(state.isLoggedIn).toBe(false);
            expect(state.account.id).toBe('');
            expect(state.loading).toBe(false);
            expect(state.error).toBeNull();
            expect(state.sessionChecked).toBe(true);
        });
    });

    describe('clearError', () => {
        it('clears error message', () => {
            const state = accountReducer({ ...initialState, error: 'oops' }, clearError());
            expect(state.error).toBeNull();
        });
    });
});
