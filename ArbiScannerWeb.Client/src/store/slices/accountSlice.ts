import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { type AccountModel, createEmptyAccountModel } from '../../types/accountType';

export interface AccountState {
    account: AccountModel;
    isLoggedIn: boolean;
    loading: boolean;
    error: string | null;
    emailConfirmToken: string | null;
    needsEmailConfirmation: boolean;
    sessionChecked: boolean;
}

const initialState: AccountState = {
    account: createEmptyAccountModel(),
    isLoggedIn: false,
    loading: false,
    error: null,
    emailConfirmToken: null,
    needsEmailConfirmation: false,
    sessionChecked: false,
};

const accountSlice = createSlice({
    name: 'account',
    initialState,
    reducers: {
        setAuthenticatedAccount(state, action: PayloadAction<AccountModel>) {
            state.account = action.payload;
            state.isLoggedIn = true;
            state.loading = false;
            state.error = null;
            state.emailConfirmToken = null;
            state.needsEmailConfirmation = false;
            state.sessionChecked = true;
        },
        requireEmailConfirmation(state, action: PayloadAction<string | null>) {
            state.account = createEmptyAccountModel();
            state.isLoggedIn = false;
            state.loading = false;
            state.emailConfirmToken = action.payload;
            state.needsEmailConfirmation = true;
            state.sessionChecked = true;
        },
        setLoading(state, action: PayloadAction<boolean>) {
            state.loading = action.payload;
        },
        setError(state, action: PayloadAction<string | null>) {
            state.error = action.payload;
        },
        markSessionChecked(state) {
            state.sessionChecked = true;
        },
        logout(state) {
            state.isLoggedIn = false;
            state.account = createEmptyAccountModel();
            state.emailConfirmToken = null;
            state.needsEmailConfirmation = false;
            state.loading = false;
            state.error = null;
            state.sessionChecked = true;
        },
        clearEmailConfirmation(state) {
            state.emailConfirmToken = null;
            state.needsEmailConfirmation = false;
        },
        clearUserData(state) {
            state.account = createEmptyAccountModel();
            state.isLoggedIn = false;
            state.emailConfirmToken = null;
            state.needsEmailConfirmation = false;
            state.error = null;
            state.loading = false;
            localStorage.clear();
        },
        clearError(state) {
            state.error = null;
        }
    }
});


export const {
    setAuthenticatedAccount,
    requireEmailConfirmation,
    setLoading,
    setError,
    markSessionChecked,
    logout,
    clearEmailConfirmation,
    clearUserData,
    clearError
} = accountSlice.actions;
export default accountSlice.reducer;