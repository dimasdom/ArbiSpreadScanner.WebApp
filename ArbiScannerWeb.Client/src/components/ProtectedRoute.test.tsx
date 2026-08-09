import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
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

const signinRedirectMock = vi.fn();
const useAuthMock = vi.fn();

vi.mock('react-oidc-context', () => ({
    useAuth: () => useAuthMock(),
}));

import { useGetUserActiveSubscriptionsQuery } from '../store/services/subscription';
const mockUseSubscription = useGetUserActiveSubscriptionsQuery as Mock;

function buildStore() {
    return configureStore({
        reducer: {
            account: accountReducer,
            [accountApi.reducerPath]: accountApi.reducer,
            [spreadApi.reducerPath]: spreadApi.reducer,
            [subscriptionsAPI.reducerPath]: subscriptionsAPI.reducer,
        },
        middleware: (getDefault) =>
            getDefault({ serializableCheck: false }).concat(
                accountApi.middleware,
                spreadApi.middleware,
                subscriptionsAPI.middleware,
            ),
    });
}

function renderRoute(requireSub = false) {
    return render(
        <Provider store={buildStore()}>
            <MemoryRouter>
                <ProtectedRoute requireActiveSubscription={requireSub}>
                    <p>Protected content</p>
                </ProtectedRoute>
            </MemoryRouter>
        </Provider>,
    );
}

describe('ProtectedRoute', () => {
    beforeEach(() => {
        signinRedirectMock.mockClear();
    });

    it('shows spinner while auth state is still loading', () => {
        useAuthMock.mockReturnValue({ isLoading: true, isAuthenticated: false, signinRedirect: signinRedirectMock });
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: false, isFetching: false });
        renderRoute();
        expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
        expect(document.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('redirects to Keycloak login when not authenticated', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: false, signinRedirect: signinRedirectMock });
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: false, isFetching: false });
        renderRoute();
        expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
        expect(signinRedirectMock).toHaveBeenCalledOnce();
    });

    it('renders children when authenticated', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: true, signinRedirect: signinRedirectMock });
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: false, isFetching: false });
        renderRoute();
        expect(screen.getByText('Protected content')).toBeInTheDocument();
    });

    it('shows spinner while active subscription is loading', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: true, signinRedirect: signinRedirectMock });
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: true, isFetching: false });
        renderRoute(true);
        expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
        expect(document.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('shows spinner while active subscription is fetching', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: true, signinRedirect: signinRedirectMock });
        mockUseSubscription.mockReturnValue({ data: undefined, isLoading: false, isFetching: true });
        renderRoute(true);
        expect(document.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('redirects to /subscriptions when subscription is not active', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: true, signinRedirect: signinRedirectMock });
        const inactiveData: FluentResult<UserSubscriptionModelDTO> = {
            isSuccess: true,
            isFailed: false,
            errors: [],
            reasons: [],
            value: { id: 1, userId: 'u1', subscriptionId: 1, startDate: '', endDate: '', isActive: false },
        };
        mockUseSubscription.mockReturnValue({ data: inactiveData, isLoading: false, isFetching: false });
        renderRoute(true);
        expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
    });

    it('renders children when subscription is active', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: true, signinRedirect: signinRedirectMock });
        const activeData: FluentResult<UserSubscriptionModelDTO> = {
            isSuccess: true,
            isFailed: false,
            errors: [],
            reasons: [],
            value: { id: 1, userId: 'u1', subscriptionId: 1, startDate: '', endDate: '', isActive: true },
        };
        mockUseSubscription.mockReturnValue({ data: activeData, isLoading: false, isFetching: false });
        renderRoute(true);
        expect(screen.getByText('Protected content')).toBeInTheDocument();
    });
});
