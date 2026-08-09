import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { type AccountModel, createEmptyAccountModel } from '../../types/accountType';

// A thin mirror of react-oidc-context's auth state (see App.tsx), kept so the
// many existing call sites reading isLoggedIn/sessionChecked from Redux don't
// all need to switch to useAuth() directly. useAuth() itself remains the
// source of truth; this slice just tracks it.
export interface AccountState {
    account: AccountModel;
    isLoggedIn: boolean;
    loading: boolean;
    error: string | null;
    sessionChecked: boolean;
}

const initialState: AccountState = {
    account: createEmptyAccountModel(),
    isLoggedIn: false,
    loading: false,
    error: null,
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
            state.loading = false;
            state.error = null;
            state.sessionChecked = true;
        },
        clearError(state) {
            state.error = null;
        }
    }
});


export const {
    setAuthenticatedAccount,
    setLoading,
    setError,
    markSessionChecked,
    logout,
    clearError
} = accountSlice.actions;
export default accountSlice.reducer;
