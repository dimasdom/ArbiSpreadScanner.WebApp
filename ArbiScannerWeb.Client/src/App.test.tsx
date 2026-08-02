import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter } from 'react-router';
import accountReducer, { clearUserData } from './store/slices/accountSlice';
import { createEmptyAccountModel } from './types/accountType';
import { createLocalStorageMock } from './test/localStorageMock';

vi.mock('./i18n/detect', () => ({ PREFERRED_LANG_KEY: 'preferredLang', getPreferredLanguage: () => 'en' }));

const { default: App } = await import('./App');

const navigateMock = vi.fn();
const logoutTriggerMock = vi.fn();
const useGetUserDataQueryMock = vi.fn();
const useGetUserActiveSubscriptionsQueryMock = vi.fn();

vi.mock('./i18n/routing', async (importOriginal) => {
    const actual = await importOriginal<typeof import('./i18n/routing')>();
    return { ...actual, useLocalizedNavigate: () => navigateMock };
});
vi.mock('./store/services/account', () => ({
    useGetUserDataQuery: (...a: unknown[]) => useGetUserDataQueryMock(...a),
    useLogoutMutation: () => [logoutTriggerMock, {}],
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
vi.mock('./components/ProtectedRoute', () => ({
    default: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock('./pages/Main/MainPage', () => ({ default: () => <div>MainPage</div> }));
vi.mock('./pages/Spread/SpreadsPage', () => ({ default: () => <div>SpreadsPage</div> }));
vi.mock('./pages/Spread/SpreadPage', () => ({ default: () => <div>SpreadPage</div> }));
vi.mock('./pages/Account/AccountPage', () => ({ default: () => <div>AccountPage</div> }));
vi.mock('./pages/Account/LoginPage', () => ({ default: () => <div>LoginPage</div> }));
vi.mock('./pages/Account/RegisterPage', () => ({ default: () => <div>RegisterPage</div> }));
vi.mock('./pages/Account/ConfirmEmailPage', () => ({ default: () => <div>ConfirmEmailPage</div> }));
vi.mock('./pages/Account/ForgotPasswordPage', () => ({ default: () => <div>ForgotPasswordPage</div> }));
vi.mock('./pages/Account/ResetPasswordPage', () => ({ default: () => <div>ResetPasswordPage</div> }));
vi.mock('./pages/Subscription/SubscriptionPage', () => ({ default: () => <div>SubscriptionPage</div> }));
vi.mock('./pages/Subscription/Payment/PaymentCryptoPage', () => ({ default: () => <div>PaymentCryptoPage</div> }));
vi.mock('./pages/Subscription/Payment/PaymentInfoPage', () => ({ default: () => <div>PaymentInfoPage</div> }));
vi.mock('./pages/Faq/FaqPage', () => ({ default: () => <div>FaqPage</div> }));

function buildStore(
    preloadedAccount: Partial<ReturnType<typeof createEmptyAccountModel>> & {
        isLoggedIn?: boolean;
        sessionChecked?: boolean;
    } = {},
) {
    const store = configureStore({
        reducer: { account: accountReducer, language: (state = { language: 'en' }) => state },
        preloadedState: {
            account: {
                account: createEmptyAccountModel(),
                isLoggedIn: preloadedAccount.isLoggedIn ?? false,
                loading: false,
                error: null,
                emailConfirmToken: null,
                needsEmailConfirmation: false,
                sessionChecked: preloadedAccount.sessionChecked ?? true,
            },
            language: { language: 'en' },
        },
    });
    return store;
}

function renderApp(path: string, isLoggedIn = false, sessionChecked = true) {
    const store = buildStore({ isLoggedIn, sessionChecked });
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
        navigateMock.mockClear();
        logoutTriggerMock.mockClear();
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
        useGetUserDataQueryMock.mockReturnValue({ data: undefined });
        useGetUserActiveSubscriptionsQueryMock.mockReturnValue({ data: undefined });
    });

    it('renders the main page for the localized root route', () => {
        renderApp('/en/');

        expect(screen.getByText('MainPage')).toBeInTheDocument();
    });

    it('renders the login page', () => {
        renderApp('/en/account/login');

        expect(screen.getByText('LoginPage')).toBeInTheDocument();
    });

    it('renders the FAQ page', () => {
        renderApp('/en/faq');

        expect(screen.getByText('FaqPage')).toBeInTheDocument();
    });

    it('renders the not-found fallback for an unknown path', () => {
        renderApp('/en/does-not-exist');

        expect(screen.getByText('notFound')).toBeInTheDocument();
    });

    it('passes login state and active subscription down to NavBar', () => {
        useGetUserActiveSubscriptionsQueryMock.mockReturnValue({ data: { isSuccess: true, value: { isActive: true } } });
        renderApp('/en/', true);

        expect(screen.getByTestId('navbar-state')).toHaveTextContent('true-true');
    });

    it('clears user data and navigates to login when onLogin fires', async () => {
        const { dispatchSpy } = renderApp('/en/');

        await userEvent.click(screen.getByRole('button', { name: 'do-login' }));

        expect(dispatchSpy).toHaveBeenCalledWith(clearUserData());
        expect(navigateMock).toHaveBeenCalledWith('/account/login');
    });

    it('triggers the logout mutation when onLogout fires', async () => {
        renderApp('/en/', true);

        await userEvent.click(screen.getByRole('button', { name: 'do-logout' }));

        expect(logoutTriggerMock).toHaveBeenCalledOnce();
    });

    it('marks the session as checked when there is no stored session', () => {
        const { dispatchSpy } = renderApp('/en/');

        expect(dispatchSpy).toHaveBeenCalledWith(expect.objectContaining({ type: 'account/markSessionChecked' }));
    });

    it('does not mark the session checked again when a session was already stored', () => {
        const storage = createLocalStorageMock();
        storage.setItem('arbiscanner.has_session', 'true');
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(storage);

        const { dispatchSpy } = renderApp('/en/');

        expect(dispatchSpy).not.toHaveBeenCalledWith(expect.objectContaining({ type: 'account/markSessionChecked' }));
    });

    it('shows the auth loading overlay while a returning session is still being checked', () => {
        const storage = createLocalStorageMock();
        storage.setItem('arbiscanner.has_session', 'true');
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(storage);

        renderApp('/en/', false, false);

        expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('hides the auth loading overlay once the session check has completed (e.g. after a 403 -> refresh -> retry cycle)', () => {
        const storage = createLocalStorageMock();
        storage.setItem('arbiscanner.has_session', 'true');
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(storage);

        renderApp('/en/', true, true);

        expect(screen.queryByRole('status')).not.toBeInTheDocument();
        expect(screen.getByTestId('navbar-state')).toHaveTextContent('true-false');
    });

    it('does not show the auth loading overlay for a fresh visitor with no stored session', () => {
        renderApp('/en/', false, false);

        expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });
});
