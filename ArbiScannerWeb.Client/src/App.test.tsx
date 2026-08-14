import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter } from 'react-router';
import accountReducer from './store/slices/accountSlice';
import { createEmptyAccountModel } from './types/accountType';

vi.mock('./i18n/detect', () => ({ PREFERRED_LANG_KEY: 'preferredLang', getPreferredLanguage: () => 'en' }));

const { default: App } = await import('./App');

const signinRedirectMock = vi.fn();
const signoutRedirectMock = vi.fn();
const useAuthMock = vi.fn();
const useGetUserDataQueryMock = vi.fn();
const useGetUserActiveSubscriptionsQueryMock = vi.fn();

vi.mock('react-oidc-context', () => ({
    useAuth: () => useAuthMock(),
}));
vi.mock('./store/services/account', () => ({
    useGetUserDataQuery: (...a: unknown[]) => useGetUserDataQueryMock(...a),
}));
vi.mock('./store/services/subscription', () => ({
    useGetUserActiveSubscriptionsQuery: (...a: unknown[]) => useGetUserActiveSubscriptionsQueryMock(...a),
}));
vi.mock('./components/NavBar', () => ({
    default: (props: { isLoggedIn: boolean; isActiveSubscription: boolean; onLogin: () => void; onLogout: () => void }) => (
        <div>
            <span data-testid="navbar-state">{`${props.isLoggedIn}-${props.isActiveSubscription}`}</span>
            <button type="button" onClick={props.onLogin}>do-login</button>
            <button type="button" onClick={props.onLogout}>do-logout</button>
        </div>
    ),
}));
vi.mock('./components/Footer', () => ({ default: () => <div data-testid="footer" /> }));
vi.mock('./components/CookieConsentModal', () => ({ default: () => <div data-testid="cookie-modal" /> }));
vi.mock('./components/ChatWidget', () => ({ default: () => <div data-testid="chat-widget" /> }));
vi.mock('./components/ProtectedRoute', () => ({
    default: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock('./pages/Main/MainPage', () => ({ default: () => <div>MainPage</div> }));
vi.mock('./pages/Spread/SpreadsPage', () => ({ default: () => <div>SpreadsPage</div> }));
vi.mock('./pages/Spread/SpreadPage', () => ({ default: () => <div>SpreadPage</div> }));
vi.mock('./pages/Stats/StatsPage', () => ({ default: () => <div>StatsPage</div> }));
vi.mock('./pages/Account/AccountPage', () => ({ default: () => <div>AccountPage</div> }));
vi.mock('./pages/Auth/AuthCallbackPage', () => ({ default: () => <div>AuthCallbackPage</div> }));
vi.mock('./pages/Subscription/SubscriptionPage', () => ({ default: () => <div>SubscriptionPage</div> }));
vi.mock('./pages/Subscription/Payment/PaymentCryptoPage', () => ({ default: () => <div>PaymentCryptoPage</div> }));
vi.mock('./pages/Subscription/Payment/PaymentInfoPage', () => ({ default: () => <div>PaymentInfoPage</div> }));
vi.mock('./pages/Faq/FaqPage', () => ({ default: () => <div>FaqPage</div> }));
vi.mock('./pages/NotFound/NotFoundPage', () => ({ default: () => <div>NotFoundPage</div> }));

function buildStore(isLoggedIn = false) {
    const store = configureStore({
        reducer: { account: accountReducer, language: (state = { language: 'en' }) => state },
        preloadedState: {
            account: {
                account: createEmptyAccountModel(),
                isLoggedIn,
                loading: false,
                error: null,
                sessionChecked: true,
            },
            language: { language: 'en' },
        },
    });
    return store;
}

function renderApp(path: string, isLoggedIn = false) {
    const store = buildStore(isLoggedIn);
    const dispatchSpy = vi.spyOn(store, 'dispatch');
    render(
        <Provider store={store}>
            <MemoryRouter initialEntries={[path]}>
                <App />
            </MemoryRouter>
        </Provider>,
    );
    return { store, dispatchSpy };
}

describe('App', () => {
    beforeEach(() => {
        signinRedirectMock.mockClear();
        signoutRedirectMock.mockClear();
        useAuthMock.mockReturnValue({
            isLoading: false,
            isAuthenticated: false,
            signinRedirect: signinRedirectMock,
            signoutRedirect: signoutRedirectMock,
        });
        useGetUserDataQueryMock.mockReturnValue({ data: undefined });
        useGetUserActiveSubscriptionsQueryMock.mockReturnValue({ data: undefined });
    });

    it('renders the main page for the localized root route', () => {
        renderApp('/en/');

        expect(screen.getByText('MainPage')).toBeInTheDocument();
    });

    it('renders the auth callback page', () => {
        renderApp('/auth/callback');

        expect(screen.getByText('AuthCallbackPage')).toBeInTheDocument();
    });

    it('renders the FAQ page', () => {
        renderApp('/en/faq');

        expect(screen.getByText('FaqPage')).toBeInTheDocument();
    });

    it('renders the stats page', () => {
        renderApp('/en/stats');

        expect(screen.getByText('StatsPage')).toBeInTheDocument();
    });

    it('renders the not-found fallback for an unknown path', () => {
        renderApp('/en/does-not-exist');

        expect(screen.getByText('NotFoundPage')).toBeInTheDocument();
    });

    it('passes login state and active subscription down to NavBar', () => {
        useAuthMock.mockReturnValue({
            isLoading: false,
            isAuthenticated: true,
            signinRedirect: signinRedirectMock,
            signoutRedirect: signoutRedirectMock,
        });
        useGetUserActiveSubscriptionsQueryMock.mockReturnValue({ data: { isSuccess: true, value: { isActive: true } } });
        renderApp('/en/', true);

        expect(screen.getByTestId('navbar-state')).toHaveTextContent('true-true');
    });

    it('triggers signinRedirect when onLogin fires', async () => {
        renderApp('/en/');

        await userEvent.click(screen.getByRole('button', { name: 'do-login' }));

        expect(signinRedirectMock).toHaveBeenCalledOnce();
    });

    it('triggers signoutRedirect when onLogout fires', async () => {
        renderApp('/en/', true);

        await userEvent.click(screen.getByRole('button', { name: 'do-logout' }));

        expect(signoutRedirectMock).toHaveBeenCalledOnce();
    });

    it('marks the session as checked once oidc auth state resolves', () => {
        const { dispatchSpy } = renderApp('/en/');

        expect(dispatchSpy).toHaveBeenCalledWith(expect.objectContaining({ type: 'account/markSessionChecked' }));
    });

    it('logs out the Redux mirror when oidc reports unauthenticated', () => {
        const { dispatchSpy } = renderApp('/en/');

        expect(dispatchSpy).toHaveBeenCalledWith(expect.objectContaining({ type: 'account/logout' }));
    });

    it('shows the auth loading overlay while oidc auth state is still resolving', () => {
        useAuthMock.mockReturnValue({
            isLoading: true,
            isAuthenticated: false,
            signinRedirect: signinRedirectMock,
            signoutRedirect: signoutRedirectMock,
        });

        renderApp('/en/');

        expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('hides the auth loading overlay once oidc auth state has resolved', () => {
        useAuthMock.mockReturnValue({
            isLoading: false,
            isAuthenticated: true,
            signinRedirect: signinRedirectMock,
            signoutRedirect: signoutRedirectMock,
        });

        renderApp('/en/', true);

        expect(screen.queryByRole('status')).not.toBeInTheDocument();
        expect(screen.getByTestId('navbar-state')).toHaveTextContent('true-false');
    });

    it('keeps the auth loading overlay up while GetUserData is still loading after oidc resolves', () => {
        useAuthMock.mockReturnValue({
            isLoading: false,
            isAuthenticated: true,
            signinRedirect: signinRedirectMock,
            signoutRedirect: signoutRedirectMock,
        });
        useGetUserDataQueryMock.mockReturnValue({ data: undefined, isLoading: true, isUninitialized: false });

        renderApp('/en/', false);

        expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('keeps the auth loading overlay up while GetUserActiveSubscriptions is still loading after GetUserData resolves', () => {
        useAuthMock.mockReturnValue({
            isLoading: false,
            isAuthenticated: true,
            signinRedirect: signinRedirectMock,
            signoutRedirect: signoutRedirectMock,
        });
        useGetUserDataQueryMock.mockReturnValue({ data: undefined, isLoading: false, isUninitialized: false });
        useGetUserActiveSubscriptionsQueryMock.mockReturnValue({ data: undefined, isLoading: true, isUninitialized: false });

        renderApp('/en/', true);

        expect(screen.getByRole('status')).toBeInTheDocument();
    });
});
