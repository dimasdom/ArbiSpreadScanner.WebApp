import type { ReactNode } from 'react';
import { useEffect } from 'react';
import { Navigate } from 'react-router';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { useGetUserActiveSubscriptionsQuery } from '../store/services/subscription';
import { withLang } from '../i18n/routing';

interface ProtectedRouteProps {
    children: ReactNode;
    requireActiveSubscription?: boolean;
}

function ProtectedRoute({ children, requireActiveSubscription = false }: Readonly<ProtectedRouteProps>) {
    const { i18n } = useTranslation();
    const auth = useAuth();
    const { data: activeSubscriptionData, isLoading, isFetching } = useGetUserActiveSubscriptionsQuery(undefined, {
        skip: !auth.isAuthenticated || !requireActiveSubscription,
    });

    useEffect(() => {
        if (!auth.isLoading && !auth.isAuthenticated) {
            void auth.signinRedirect();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [auth.isLoading, auth.isAuthenticated]);

    if (auth.isLoading || !auth.isAuthenticated) {
        return <div className="flex items-center justify-center min-h-64 mt-6"><div className="w-10 h-10 border-4 border-gray-200 border-t-indigo-500 rounded-full animate-spin" /></div>;
    }

    if (requireActiveSubscription && (isLoading || isFetching)) {
        return <div className="flex items-center justify-center min-h-64 mt-6"><div className="w-10 h-10 border-4 border-gray-200 border-t-indigo-500 rounded-full animate-spin" /></div>;
    }

    if (requireActiveSubscription && !activeSubscriptionData?.value?.isActive) {
        return <Navigate to={withLang('/subscriptions', i18n.language)} replace />;
    }

    return <>{children}</>;
}

export default ProtectedRoute;
