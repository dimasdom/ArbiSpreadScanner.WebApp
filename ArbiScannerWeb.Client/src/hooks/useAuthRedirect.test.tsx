import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useAuthRedirect } from './useAuthRedirect';

const signinRedirectMock = vi.fn();
const useAuthMock = vi.fn();

vi.mock('react-oidc-context', () => ({
    useAuth: () => useAuthMock(),
}));

describe('useAuthRedirect', () => {
    beforeEach(() => {
        signinRedirectMock.mockClear();
        useAuthMock.mockReset();
    });

    it('redirects to Keycloak login when not authenticated', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: false, signinRedirect: signinRedirectMock });

        renderHook(() => useAuthRedirect());

        expect(signinRedirectMock).toHaveBeenCalledOnce();
    });

    it('does not redirect while auth state is still loading', () => {
        useAuthMock.mockReturnValue({ isLoading: true, isAuthenticated: false, signinRedirect: signinRedirectMock });

        renderHook(() => useAuthRedirect());

        expect(signinRedirectMock).not.toHaveBeenCalled();
    });

    it('does not redirect when already authenticated', () => {
        useAuthMock.mockReturnValue({ isLoading: false, isAuthenticated: true, signinRedirect: signinRedirectMock });

        renderHook(() => useAuthRedirect());

        expect(signinRedirectMock).not.toHaveBeenCalled();
    });
});
