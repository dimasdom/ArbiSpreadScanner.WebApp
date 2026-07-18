import type { ReactNode } from 'react';
import { Navigate } from 'react-router';
import { useSelector } from 'react-redux';
import { useTranslation } from 'react-i18next';
import type { IRootStore } from '../store/store';
import { useGetUserActiveSubscriptionsQuery } from '../store/services/subscription';
import { withLang } from '../i18n/routing';

interface ProtectedRouteProps {
    children: ReactNode;
    requireActiveSubscription?: boolean;
}

function ProtectedRoute({ children, requireActiveSubscription = false }: Readonly<ProtectedRouteProps>) {
    const { i18n } = useTranslation();
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const sessionChecked = useSelector((state: IRootStore) => state.account.sessionChecked);
    const { data: activeSubscriptionData, isLoading, isFetching } = useGetUserActiveSubscriptionsQuery(undefined, {
        skip: !isLoggedIn || !requireActiveSubscription,
    });

    if (!sessionChecked) {
        return <div className="flex items-center justify-center min-h-64 mt-6"><div className="w-10 h-10 border-4 border-gray-200 border-t-indigo-500 rounded-full animate-spin" /></div>;
    }

    if (!isLoggedIn) {
        return <Navigate to={withLang('/account/login', i18n.language)} replace />;
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
