import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import accountReducer from '../../store/slices/accountSlice';
import { createEmptyAccountModel } from '../../types/accountType';
import { createLocalStorageMock } from '../../test/localStorageMock';
import MainPage from './MainPage';

const navigateMock = vi.fn();

vi.mock('../../i18n/routing', async (importOriginal) => {
    const actual = await importOriginal<typeof import('../../i18n/routing')>();
    return { ...actual, useLocalizedNavigate: () => navigateMock };
});
vi.mock('./LiveDemoWidget', () => ({ default: () => <div data-testid="live-demo-widget" /> }));

function renderMainPage(isLoggedIn: boolean, sessionChecked: boolean) {
    const store = configureStore({
        reducer: { account: accountReducer },
        preloadedState: {
            account: {
                account: createEmptyAccountModel(), isLoggedIn, loading: false, error: null,
                emailConfirmToken: null, needsEmailConfirmation: false, sessionChecked,
            },
        },
    });
    return render(<Provider store={store}><MainPage /></Provider>);
}

describe('MainPage', () => {
    beforeEach(() => {
        vi.spyOn(globalThis, 'localStorage', 'get').mockReturnValue(createLocalStorageMock());
        sessionStorage.clear();
        navigateMock.mockClear();
    });

    it('shows the live demo widget for a logged-out session', () => {
        renderMainPage(false, true);

        expect(screen.getByTestId('live-demo-widget')).toBeInTheDocument();
    });

    it('hides the live demo widget once logged in', () => {
        renderMainPage(true, true);

        expect(screen.queryByTestId('live-demo-widget')).not.toBeInTheDocument();
    });

    it('does not render the demo widget before the session check completes', () => {
        renderMainPage(false, false);

        expect(screen.queryByTestId('live-demo-widget')).not.toBeInTheDocument();
    });

    it('redirects a logged-in user to /spreads once per session', () => {
        renderMainPage(true, true);

        expect(navigateMock).toHaveBeenCalledWith('/spreads');
        expect(sessionStorage.getItem('redirected_to_spreads_this_session')).toBe('true');
    });

    it('does not redirect again within the same session', () => {
        sessionStorage.setItem('redirected_to_spreads_this_session', 'true');

        renderMainPage(true, true);

        expect(navigateMock).not.toHaveBeenCalled();
    });

    it('does not redirect before the session check completes', () => {
        renderMainPage(true, false);

        expect(navigateMock).not.toHaveBeenCalled();
    });

    it('renders the exchange list and feature cards', () => {
        renderMainPage(false, true);

        expect(screen.getByText('exchanges.heading')).toBeInTheDocument();
        expect(screen.getByText('Binance')).toBeInTheDocument();
        expect(screen.getByText('features.personalize.title')).toBeInTheDocument();
    });
});
