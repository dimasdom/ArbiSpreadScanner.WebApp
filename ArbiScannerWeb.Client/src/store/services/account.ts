import { createApi } from '@reduxjs/toolkit/query/react';
import type { AccountDTO, AccountUpdateDTO, EmailConfirmationCodes, TelegramLinkRequest, UserSubscriptionModelDTO } from '../../types/accountType';
import type { FluentResult } from '../../types/FluentResultType';
import {
    clearError,
    logout,
    requireEmailConfirmation,
    setAuthenticatedAccount,
    setError,
    setLoading,
} from '../slices/accountSlice';
import { baseQueryWithReauth } from './baseQuery';

interface AccountLoginDTO {
    login: string;
    password: string;
}

const getResultMessage = (result: FluentResult<unknown> | FluentResult | undefined, fallback: string) => {
    return result?.errors?.[0]?.message || result?.reasons?.[0]?.message || fallback;
};

const getErrorMessage = (error: unknown, fallback: string) => {
    if (typeof error === 'object' && error !== null && 'error' in error) {
        const nestedError = (error as { error?: unknown }).error;
        if (typeof nestedError === 'string') {
            return nestedError;
        }
    }

    return fallback;
};

export const accountApi = createApi({
    reducerPath: 'accountApi',
    baseQuery: baseQueryWithReauth,
    tagTypes: ['Account'],
    endpoints: (builder) => ({
        login: builder.mutation<FluentResult<AccountDTO>, AccountLoginDTO>({
            query: (payload) => ({
                url: '/Account/Login',
                method: 'POST',
                body: payload,
            }),
            async onQueryStarted(_, { dispatch, queryFulfilled }) {
                dispatch(setLoading(true));
                dispatch(clearError());

                try {
                    const { data } = await queryFulfilled;
                    if (!data.isSuccess) {
                        dispatch(setError(getResultMessage(data, 'Login failed')));
                        return;
                    }

                    if (!data.value.emailConfirmed) {
                        dispatch(requireEmailConfirmation(data.value.emailConfirmToken || null));
                        return;
                    }

                    dispatch(setAuthenticatedAccount(data.value));
                } catch (error) {
                    dispatch(setError(getErrorMessage(error, 'Login failed')));
                    dispatch(setLoading(false));
                }
            },
        }),
        register: builder.mutation<FluentResult<EmailConfirmationCodes>, AccountLoginDTO>({
            query: (payload) => ({
                url: '/Account/Register',
                method: 'POST',
                body: payload,
            }),
        }),
        forgotPassword: builder.mutation<FluentResult, { email: string }>({
            query: (payload) => ({
                url: '/Account/ForgotPassword',
                method: 'POST',
                body: payload,
            }),
        }),
        resetPassword: builder.mutation<FluentResult, { token: string; newPassword: string }>({
            query: (payload) => ({
                url: '/Account/ResetPassword',
                method: 'POST',
                body: payload,
            }),
        }),
        confirmEmail: builder.mutation<FluentResult, { emailConfirmToken: string; token: string }>({
            query: (payload) => ({
                url: '/Account/ConfirmEmail',
                method: 'POST',
                body: payload,
            }),
        }),
        resendEmailRequest: builder.mutation<FluentResult, { emailConfirmToken: string }>({
            query: (payload) => ({
                url: '/Account/ResendEmailConfirmation',
                method: 'POST',
                body: payload,
            }),
        }),
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
        changeEmailRequest: builder.mutation<FluentResult<EmailConfirmationCodes>, { email: string }>({
            query: (payload) => ({
                url: '/Account/ChangeEmailRequest',
                method: 'POST',
                body: payload,
            }),
        }),
        createTelegramLinkRequest: builder.mutation<FluentResult<TelegramLinkRequest>, void>({
            query: () => ({
                url: '/Account/CreateTelegramLinkRequest',
                method: 'POST',
            }),
        }),
        removeTelegramLink: builder.mutation<FluentResult, void>({
            query: () => ({
                url: '/Account/RemoveTelegramLink',
                method: 'POST',
            }),
            invalidatesTags: ['Account'],
        }),
        logout: builder.mutation<FluentResult, void>({
            query: () => ({
                url: '/Account/Logout',
                method: 'POST',
                body: {},
            }),
            async onQueryStarted(_, { dispatch, queryFulfilled }) {
                try {
                    await queryFulfilled;
                } finally {
                    dispatch(logout());
                }
            },
        }),
        getUserActiveSubscription: builder.query<FluentResult<UserSubscriptionModelDTO>, void>({
            query: () => ({
                url: '/Subscription/GetUserActiveSubscriptions',
                method: 'GET',
            }),
        }),
    }),
});

export const {
    useLoginMutation,
    useRegisterMutation,
    useForgotPasswordMutation,
    useResetPasswordMutation,
    useConfirmEmailMutation,
    useResendEmailRequestMutation,
    useGetUserDataQuery,
    useUpdateAccountDetailsMutation,
    useChangeEmailRequestMutation,
    useCreateTelegramLinkRequestMutation,
    useRemoveTelegramLinkMutation,
    useLogoutMutation,
} = accountApi;
