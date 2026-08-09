import { createApi } from '@reduxjs/toolkit/query/react';
import type { AccountDTO, AccountUpdateDTO, TelegramLinkRequest } from '../../types/accountType';
import type { FluentResult } from '../../types/FluentResultType';
import {
    clearError,
    logout,
    setAuthenticatedAccount,
    setError,
    setLoading,
} from '../slices/accountSlice';
import { baseQueryWithReauth } from './baseQuery';
import { normalizeApiError } from '../../utils/normalizeApiError';

const getResultMessage = (result: FluentResult<unknown> | FluentResult | undefined, fallback: string) => {
    return result?.errors?.[0]?.message || result?.reasons?.[0]?.message || fallback;
};

const getErrorMessage = (rejection: unknown, fallback: string) => {
    const nestedError =
        typeof rejection === 'object' && rejection !== null && 'error' in rejection
            ? (rejection as { error: unknown }).error
            : rejection;

    return normalizeApiError(nestedError as Parameters<typeof normalizeApiError>[0]).message || fallback;
};

// Login/Register/ForgotPassword/ResetPassword/ConfirmEmail/Logout are gone —
// Keycloak owns the whole auth lifecycle now (see react-oidc-context's
// useAuth() in App.tsx/ProtectedRoute.tsx). What's left just reads/writes
// business data for the authenticated, JIT-provisioned user.
export const accountApi = createApi({
    reducerPath: 'accountApi',
    baseQuery: baseQueryWithReauth,
    tagTypes: ['Account'],
    endpoints: (builder) => ({
        getUserData: builder.query<FluentResult<AccountDTO>, void>({
            query: () => ({
                url: '/Account/GetUserData',
                method: 'GET',
            }),
            providesTags: ['Account'],
            async onQueryStarted(_, { dispatch, queryFulfilled }) {
                dispatch(setLoading(true));

                try {
                    const { data } = await queryFulfilled;
                    if (data.isSuccess) {
                        dispatch(setAuthenticatedAccount(data.value));
                    } else {
                        dispatch(logout());
                        dispatch(setError(getResultMessage(data, 'Failed to load user data')));
                    }
                } catch {
                    dispatch(logout());
                }
            },
        }),
        updateAccountDetails: builder.mutation<FluentResult<AccountUpdateDTO>, AccountUpdateDTO>({
            query: (payload) => ({
                url: '/Account/UpdateDetails',
                method: 'POST',
                body: payload,
            }),
            invalidatesTags: ['Account'],
            async onQueryStarted(_, { dispatch, queryFulfilled }) {
                dispatch(setLoading(true));
                dispatch(clearError());

                try {
                    const { data } = await queryFulfilled;
                    if (!data.isSuccess) {
                        dispatch(setError(getResultMessage(data, 'Update failed')));
                        dispatch(setLoading(false));
                    }
                } catch (error) {
                    dispatch(setError(getErrorMessage(error, 'Update failed')));
                    dispatch(setLoading(false));
                }
            },
        }),
        createTelegramLinkRequest: builder.mutation<FluentResult<TelegramLinkRequest>, void>({
            query: () => ({
                url: '/TelegramLink/CreateTelegramLinkRequest',
                method: 'POST',
            }),
        }),
        removeTelegramLink: builder.mutation<FluentResult, void>({
            query: () => ({
                url: '/TelegramLink/RemoveTelegramLink',
                method: 'POST',
            }),
            invalidatesTags: ['Account'],
        }),
    }),
});

export const {
    useGetUserDataQuery,
    useUpdateAccountDetailsMutation,
    useCreateTelegramLinkRequestMutation,
    useRemoveTelegramLinkMutation,
} = accountApi;
