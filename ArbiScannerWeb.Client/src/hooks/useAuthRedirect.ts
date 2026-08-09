import { useEffect } from 'react';
import { useAuth } from 'react-oidc-context';

// Same pattern as ProtectedRoute.tsx: there's no local /account/login route
// anymore, so an unauthenticated visitor goes straight to Keycloak's hosted
// login instead.
export function useAuthRedirect() {
    const auth = useAuth();

    useEffect(() => {
        if (!auth.isLoading && !auth.isAuthenticated) {
            void auth.signinRedirect();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [auth.isLoading, auth.isAuthenticated]);
}
