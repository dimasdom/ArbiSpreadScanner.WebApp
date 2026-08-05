import { useEffect } from 'react';
import { useAuth } from 'react-oidc-context';
import { useTranslation } from 'react-i18next';
import { useLocalizedNavigate } from '../../i18n/routing';

// Keycloak's redirect_uri must be one fixed, whitelisted string — incompatible
// with this app's /:lang/... route prefixing — so this route lives outside
// the LangGuard tree. AuthProvider (see main.tsx) processes the ?code=&state=
// callback automatically; this page just waits for that to finish and then
// hands off to the normal, language-prefixed app.
export function AuthCallbackPage() {
    const auth = useAuth();
    const navigate = useLocalizedNavigate();
    const { t } = useTranslation('common');

    useEffect(() => {
        if (!auth.isLoading && auth.isAuthenticated) {
            navigate('/', { replace: true });
        }
    }, [auth.isLoading, auth.isAuthenticated, navigate]);

    if (auth.error) {
        return (
            <div className="flex flex-col items-center justify-center min-h-64 mt-6 gap-4">
                <p className="text-red-600 dark:text-red-400">{t('errors.networkError')}</p>
                <p className="text-sm text-gray-500 dark:text-gray-400">{auth.error.message}</p>
            </div>
        );
    }

    return (
        <div className="flex items-center justify-center min-h-64 mt-6">
            <div className="w-10 h-10 border-4 border-gray-200 border-t-indigo-500 rounded-full animate-spin" />
        </div>
    );
}

export default AuthCallbackPage;
