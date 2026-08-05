import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import AuthCallbackPage from './AuthCallbackPage';

const navigateMock = vi.fn();
const useAuthMock = vi.fn();

vi.mock('react-oidc-context', () => ({
    useAuth: () => useAuthMock(),
}));
vi.mock('../../i18n/routing', async (importOriginal) => {
    const actual = await importOriginal<typeof import('../../i18n/routing')>();
    return { ...actual, useLocalizedNavigate: () => navigateMock };
});

describe('AuthCallbackPage', () => {
    beforeEach(() => {
        navigateMock.mockClear();
    });

    it('shows a spinner while the callback is still processing', () => {
        useAuthMock.mockReturnValue({ isLoading: true, isAuthenticated: false, error: undefined });

        render(<MemoryRouter><AuthCallbackPage /></MemoryRouter>);

        expect(document.querySelector('.animate-spin')).toBeInTheDocument();
        expect(navigateMock).not.toHaveBeenCalled();
    });

    it('navigates into the app once authenticated', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: true, error: undefined });

        render(<MemoryRouter><AuthCallbackPage /></MemoryRouter>);

        expect(navigateMock).toHaveBeenCalledWith('/', { replace: true });
    });

    it('does not navigate when the callback failed to authenticate', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: false, error: undefined });

        render(<MemoryRouter><AuthCallbackPage /></MemoryRouter>);

        expect(navigateMock).not.toHaveBeenCalled();
    });

    it('shows an error message when the callback fails', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: false, error: new Error('invalid_grant') });

        render(<MemoryRouter><AuthCallbackPage /></MemoryRouter>);

        expect(screen.getByText('invalid_grant')).toBeInTheDocument();
        expect(navigateMock).not.toHaveBeenCalled();
    });
});
