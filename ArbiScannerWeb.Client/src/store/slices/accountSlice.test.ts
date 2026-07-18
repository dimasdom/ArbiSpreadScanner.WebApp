import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import accountReducer, {
    setAuthenticatedAccount,
    requireEmailConfirmation,
    setLoading,
    setError,
    markSessionChecked,
    logout,
    clearEmailConfirmation,
    clearUserData,
    clearError,
    hasStoredSession,
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
    emailConfirmToken: null,
    needsEmailConfirmation: false,
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

describe('accountSlice reducer', () => {
    let localStorageMock: Storage;

    beforeEach(() => {
        localStorageMock = createLocalStorageMock();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(localStorageMock);
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

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

        it('clears email confirmation state', () => {
            const confirmingState: AccountState = {
                ...initialState,
                emailConfirmToken: 'token-abc',
                needsEmailConfirmation: true,
            };
            const state = accountReducer(confirmingState, setAuthenticatedAccount(testAccount));
            expect(state.emailConfirmToken).toBeNull();
            expect(state.needsEmailConfirmation).toBe(false);
        });

        it('marks a stored session so future loads know a login exists', () => {
            expect(hasStoredSession()).toBe(false);
            accountReducer(initialState, setAuthenticatedAccount(testAccount));
            expect(hasStoredSession()).toBe(true);
        });
    });

    describe('requireEmailConfirmation', () => {
        it('stores token and sets needsEmailConfirmation', () => {
            const state = accountReducer(initialState, requireEmailConfirmation('abc-token'));
            expect(state.emailConfirmToken).toBe('abc-token');
            expect(state.needsEmailConfirmation).toBe(true);
            expect(state.isLoggedIn).toBe(false);
            expect(state.sessionChecked).toBe(true);
        });

        it('accepts null token', () => {
            const state = accountReducer(initialState, requireEmailConfirmation(null));
            expect(state.emailConfirmToken).toBeNull();
            expect(state.needsEmailConfirmation).toBe(true);
        });

        it('resets account to empty', () => {
            const loggedInState: AccountState = { ...initialState, account: testAccount, isLoggedIn: true };
            const state = accountReducer(loggedInState, requireEmailConfirmation('token'));
            expect(state.account.id).toBe('');
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
                emailConfirmToken: 'token',
                needsEmailConfirmation: true,
                error: 'some error',
            };
            const state = accountReducer(loggedInState, logout());
            expect(state.isLoggedIn).toBe(false);
            expect(state.account.id).toBe('');
            expect(state.emailConfirmToken).toBeNull();
            expect(state.needsEmailConfirmation).toBe(false);
            expect(state.loading).toBe(false);
            expect(state.error).toBeNull();
            expect(state.sessionChecked).toBe(true);
        });

        it('clears the stored session marker', () => {
            localStorageMock.setItem('arbiscanner.has_session', 'true');
            accountReducer(initialState, logout());
            expect(hasStoredSession()).toBe(false);
        });
    });

    describe('clearEmailConfirmation', () => {
        it('clears token and needsEmailConfirmation flag', () => {
            const state = accountReducer(
                { ...initialState, emailConfirmToken: 'token', needsEmailConfirmation: true },
                clearEmailConfirmation(),
            );
            expect(state.emailConfirmToken).toBeNull();
            expect(state.needsEmailConfirmation).toBe(false);
        });
    });

    describe('clearUserData', () => {
        it('resets all account state', () => {
            const clearSpy = vi.fn();
            vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue({ clear: clearSpy } as unknown as Storage);

            const loggedInState: AccountState = {
                ...initialState,
                account: testAccount,
                isLoggedIn: true,
                error: 'error',
            };
            const state = accountReducer(loggedInState, clearUserData());
            expect(state.account.id).toBe('');
            expect(state.isLoggedIn).toBe(false);
            expect(state.error).toBeNull();
        });

        it('calls localStorage.clear', () => {
            const clearSpy = vi.fn();
            vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue({ clear: clearSpy } as unknown as Storage);

            accountReducer(initialState, clearUserData());
            expect(clearSpy).toHaveBeenCalledOnce();
        });
    });

    describe('clearError', () => {
        it('clears error message', () => {
            const state = accountReducer({ ...initialState, error: 'oops' }, clearError());
            expect(state.error).toBeNull();
        });
    });
});
