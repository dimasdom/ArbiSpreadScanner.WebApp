import { describe, it, expect, vi, type Mock } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter } from 'react-router';
import { configureStore } from '@reduxjs/toolkit';
import accountReducer from '../store/slices/accountSlice';
import { subscriptionsAPI } from '../store/services/subscription';
import { spreadApi } from '../store/services/spread';
import { accountApi } from '../store/services/account';
import ProtectedRoute from './ProtectedRoute';
import type { FluentResult } from '../types/FluentResultType';
import type { UserSubscriptionModelDTO } from '../types/accountType';

vi.mock('../store/services/subscription', async (importOriginal) => {
    const actual = await importOriginal<typeof import('../store/services/subscription')>();
    return {
        ...actual,
        useGetUserActiveSubscriptionsQuery: vi.fn(),
    };
});

import { useGetUserActiveSubscriptionsQuery } from '../store/services/subscription';
const mockUseSubscription = useGetUserActiveSubscriptionsQuery as Mock;

function buildStore(accountOverrides: object = {}) {
    return configureStore({
        reducer: {
            account: accountReducer,
            [accountApi.reducerPath]: accountApi.reducer,
            [spreadApi.reducerPath]: spreadApi.reducer,
            [subscriptionsAPI.reducerPath]: subscriptionsAPI.reducer,
        },
        preloadedState: {
            account: {
                account: { id: '', userName: null, email: null, emailConfirmed: false, telegramUserId: 0, userSettings: { id: 0, chatId: 0, spreadSize: 0, positionSize: 0, futuresSpread: false, fundingSpread: false, spotSpread: false, haveAccess: false, active: false, exchanges: [] } },
                isLoggedIn: false,
                loading: false,
                error: null,
                emailConfirmToken: null,
                needsEmailConfirmation: false,
                sessionChecked: false,
                ...accountOverrides,
            },
        },
        middleware: (getDefault) =>
            getDefault({ serializableCheck: false }).concat(
                accountApi.middleware,
                spreadApi.middleware,
                subscriptionsAPI.middleware,
            ),
    });
}

function renderRoute(store: ReturnType<typeof buildStore>, requireSub = false) {
    return render(
        <Provider store={store}>
            <MemoryRouter>
                <ProtectedRoute requireActiveSubscription={requireSub}>
                    <p>Protected content</p>
                </ProtectedRoute>
            </MemoryRouter>
        </Provider>,
    );
}

describe('ProtectedRoute', () => {
    it('shows spinner while session is not yet checked', () => {
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: false, isFetching: false });
        renderRoute(buildStore({ sessionChecked: false, isLoggedIn: false }));
        expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
        expect(document.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('redirects to /account/login when not logged in and session is checked', () => {
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: false, isFetching: false });
        renderRoute(buildStore({ sessionChecked: true, isLoggedIn: false }));
        expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
    });

    it('renders children when logged in', () => {
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: false, isFetching: false });
        renderRoute(buildStore({ sessionChecked: true, isLoggedIn: true }));
        expect(screen.getByText('Protected content')).toBeInTheDocument();
    });

    it('shows spinner while active subscription is loading', () => {
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: true, isFetching: false });
        renderRoute(buildStore({ sessionChecked: true, isLoggedIn: true }), true);
        expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
        expect(document.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('shows spinner while active subscription is fetching', () => {
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: false, isFetching: true });
        renderRoute(buildStore({ sessionChecked: true, isLoggedIn: true }), true);
        expect(document.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('redirects to /subscriptions when subscription is not active', () => {
        const inactiveData: FluentResult<UserSubscriptionModelDTO> = {
            isSuccess: true,
            isFailed: false,
            errors: [],
            reasons: [],
            value: { id: 1, userId: 'u1', subscriptionId: 1, startDate: '', endDate: '', isActive: false },
        };
        mockUseSubscription.mockReturnValue({ data: inactiveData, isLoading: false, isFetching: false });
        renderRoute(buildStore({ sessionChecked: true, isLoggedIn: true }), true);
        expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
    });

    it('renders children when subscription is active', () => {
        const activeData: FluentResult<UserSubscriptionModelDTO> = {
            isSuccess: true,
            isFailed: false,
            errors: [],
            reasons: [],
            value: { id: 1, userId: 'u1', subscriptionId: 1, startDate: '', endDate: '', isActive: true },
        };
        mockUseSubscription.mockReturnValue({ data: activeData, isLoading: false, isFetching: false });
        renderRoute(buildStore({ sessionChecked: true, isLoggedIn: true }), true);
        expect(screen.getByText('Protected content')).toBeInTheDocument();
    });
});
