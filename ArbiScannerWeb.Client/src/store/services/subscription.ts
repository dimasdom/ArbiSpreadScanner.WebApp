import { createApi } from '@reduxjs/toolkit/query/react'
import type { SubscriptionModel, UserSubscriptionPayment, UserSubscriptionModelDTO } from '../../types/accountType'
import type { FluentResult } from '../../types/FluentResultType'
import { baseQueryWithReauth } from './baseQuery'

export const subscriptionsAPI = createApi({
    reducerPath: 'subscriptionsAPI',
    baseQuery: baseQueryWithReauth,
    tagTypes: ['Subscriptions', 'ActiveSubscription', 'ActivePayments'],
    endpoints: (builder) => ({
        getAllSubscriptions: builder.query<FluentResult<SubscriptionModel[]>, void>({
            query: () => ({
                url: '/Subscription/GetAllSubscriptions',
                method: 'GET',
            }),
            providesTags: ['Subscriptions'],
        }),
        
        getSubscriptionDetails: builder.query<FluentResult<SubscriptionModel>, number>({
            query: (subscriptionId) => ({
                url: `/Subscription/GetSubscriptionDetails?subscriptionId=${subscriptionId}`,
                method: 'GET',
            }),
        }),

        getPaymentStatus: builder.query<FluentResult<UserSubscriptionPayment>, number>({
            query: (userSubscriptionPaymentId) => ({
                url: `/Subscription/GetPaymentStatus?userSubscriptionPaymentId=${encodeURIComponent(userSubscriptionPaymentId)}`,
                method: 'GET',
            }),
            providesTags: ['ActivePayments'],
        }),

        createPayment: builder.mutation<FluentResult<UserSubscriptionPayment>, number>({
            query: (subscriptionId) => ({
                url: `/Subscription/CreatePayment?subscriptionId=${encodeURIComponent(subscriptionId)}`,
                method: 'POST',
            }),
            invalidatesTags: ['ActivePayments'],
        }),

        cancelPayment: builder.mutation<FluentResult, number>({
            query: (userSubscriptionPaymentId) => ({
                url: `/Subscription/CancelPayment?userSubscriptionPaymentId=${encodeURIComponent(userSubscriptionPaymentId)}`,
                method: 'POST',
            }),
            invalidatesTags: ['ActivePayments'],
        }),

        getUserActiveSubscriptions: builder.query<FluentResult<UserSubscriptionModelDTO>, void>({
            query: () => ({
                url: '/Subscription/GetUserActiveSubscriptions',
                method: 'GET',
            }),
            providesTags: ['ActiveSubscription'],
        }),

        getUserActivePayments: builder.query<FluentResult<UserSubscriptionPayment>, void>({
            query: () => ({
                url: '/Subscription/GetUserActivePayments',
                method: 'GET',
            }),
            providesTags: ['ActivePayments'],
        }),
    }),
})

export const {
    useGetAllSubscriptionsQuery,
    useGetSubscriptionDetailsQuery,
    useGetPaymentStatusQuery,
    useCreatePaymentMutation,
    useGetUserActiveSubscriptionsQuery,
    useGetUserActivePaymentsQuery,
    useCancelPaymentMutation,
} = subscriptionsAPI