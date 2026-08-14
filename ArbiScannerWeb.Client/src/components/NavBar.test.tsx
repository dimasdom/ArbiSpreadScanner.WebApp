import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Provider } from 'react-redux';
import { configureStore } from '@reduxjs/toolkit';
import { MemoryRouter } from 'react-router';
import NavBar from './NavBar';

const useThemeMock = vi.fn();
const navigateMock = vi.fn();

vi.mock('../contexts/ThemeContext', () => ({ useTheme: () => useThemeMock() }));
vi.mock('react-router', async (importOriginal) => {
    const actual = await importOriginal<typeof import('react-router')>();
    return { ...actual, useNavigate: () => navigateMock };
});

function renderNavBar(props: Partial<{ isLoggedIn: boolean; isActiveSubscription: boolean; onLogin: () => void; onLogout: () => void }> = {}) {
    const store = configureStore({ reducer: { language: (state = { language: 'en' }) => state } });
    const onLogin = props.onLogin ?? vi.fn();
    const onLogout = props.onLogout ?? vi.fn();
    render(
        <Provider store={store}>
            <MemoryRouter>
                <NavBar
                    isLoggedIn={props.isLoggedIn ?? false}
                    isActiveSubscription={props.isActiveSubscription ?? false}
                    onLogin={onLogin}
                    onLogout={onLogout}
                />
            </MemoryRouter>
        </Provider>,
    );
    return { onLogin, onLogout };
}

describe('NavBar', () => {
    beforeEach(() => {
        useThemeMock.mockReturnValue({ theme: 'light', toggleTheme: vi.fn() });
        navigateMock.mockClear();
    });

    it('shows the default nav links when logged out, including stats (public)', () => {
        renderNavBar({ isLoggedIn: false });

        expect(screen.getAllByText('nav.subscriptions').length).toBeGreaterThan(0);
        expect(screen.getAllByText('nav.stats').length).toBeGreaterThan(0);
        expect(screen.queryAllByText('nav.account')).toHaveLength(0);
        expect(screen.queryAllByText('nav.spreads')).toHaveLength(0);
    });

    it('shows the account/subscriptions links when logged in without an active subscription, including stats (public)', () => {
        renderNavBar({ isLoggedIn: true, isActiveSubscription: false });

        expect(screen.getAllByText('nav.account').length).toBeGreaterThan(0);
        expect(screen.getAllByText('nav.subscriptions').length).toBeGreaterThan(0);
        expect(screen.getAllByText('nav.stats').length).toBeGreaterThan(0);
        expect(screen.queryAllByText('nav.spreads')).toHaveLength(0);
    });

    it('shows the spreads and stats links instead of subscriptions when logged in with an active subscription', () => {
        renderNavBar({ isLoggedIn: true, isActiveSubscription: true });

        expect(screen.getAllByText('nav.spreads').length).toBeGreaterThan(0);
        expect(screen.getAllByText('nav.stats').length).toBeGreaterThan(0);
        expect(screen.getAllByText('nav.account').length).toBeGreaterThan(0);
        expect(screen.queryAllByText('nav.subscriptions')).toHaveLength(0);
    });

    it('shows a login button when logged out and calls onLogin when clicked', async () => {
        const { onLogin } = renderNavBar({ isLoggedIn: false });

        await userEvent.click(screen.getAllByText('auth.logIn')[0]);

        expect(onLogin).toHaveBeenCalledOnce();
    });

    it('shows a logout button when logged in and calls onLogout when clicked', async () => {
        const { onLogout } = renderNavBar({ isLoggedIn: true });

        await userEvent.click(screen.getAllByText('auth.logOut')[0]);

        expect(onLogout).toHaveBeenCalledOnce();
    });

    it('calls toggleTheme when the theme button is clicked', async () => {
        const toggleTheme = vi.fn();
        useThemeMock.mockReturnValue({ theme: 'light', toggleTheme });
        renderNavBar();

        await userEvent.click(screen.getByRole('button', { name: 'theme.toggleAria' }));

        expect(toggleTheme).toHaveBeenCalledOnce();
    });

    it('opens and closes the mobile side menu', async () => {
        renderNavBar();
        const sideMenu = () => screen.getByText('nav.menu').closest('div')!.parentElement!;

        await userEvent.click(screen.getByRole('button', { name: 'nav.toggleMenu' }));
        expect(sideMenu()).toHaveClass('translate-x-0');

        await userEvent.click(screen.getByRole('button', { name: 'nav.closeMenu' }));
        expect(sideMenu()).toHaveClass('-translate-x-full');
    });

    it('closes the mobile side menu when the backdrop is clicked', async () => {
        renderNavBar();
        await userEvent.click(screen.getByRole('button', { name: 'nav.toggleMenu' }));
        const backdrop = document.querySelector('.bg-opacity-50');
        expect(backdrop).not.toBeNull();

        await userEvent.click(backdrop!);

        expect(screen.queryByText('nav.menu')?.closest('div')?.parentElement).toHaveClass('-translate-x-full');
    });

    it('closes the side menu after logging out from it', async () => {
        const onLogout = vi.fn();
        renderNavBar({ isLoggedIn: true, onLogout });
        await userEvent.click(screen.getByRole('button', { name: 'nav.toggleMenu' }));

        const logoutButtons = screen.getAllByText('auth.logOut');
        await userEvent.click(logoutButtons[logoutButtons.length - 1]);

        expect(onLogout).toHaveBeenCalledOnce();
    });
});
