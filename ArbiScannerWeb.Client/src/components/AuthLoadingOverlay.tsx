import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

export const AUTH_LOADING_OVERLAY_TRANSITION_MS = 300;

interface AuthLoadingOverlayProps {
    show: boolean;
}

export default function AuthLoadingOverlay({ show }: Readonly<AuthLoadingOverlayProps>) {
    const { t } = useTranslation('common');
    const [rendered, setRendered] = useState(show);

    useEffect(() => {
        if (show) {
            setRendered(true);
            return;
        }

        const timeoutId = window.setTimeout(() => setRendered(false), AUTH_LOADING_OVERLAY_TRANSITION_MS);
        return () => window.clearTimeout(timeoutId);
    }, [show]);

    if (!rendered) {
        return null;
    }

    return (
        <div
            role="status"
            aria-live="polite"
            className={`fixed inset-0 z-100 flex flex-col items-center justify-center gap-4 bg-white dark:bg-gray-950 transition-opacity ease-out ${
                show ? 'opacity-100' : 'opacity-0'
            }`}
            style={{ transitionDuration: `${AUTH_LOADING_OVERLAY_TRANSITION_MS}ms` }}
        >
            <p className="max-w-xs sm:max-w-lg text-center px-4 text-2xl sm:text-4xl md:text-5xl font-bold leading-tight text-blue-600 dark:text-blue-400">
                {t('loading.tradingOpportunities')}
            </p>
            <div
                className="w-10 h-10 sm:w-14 sm:h-14 border-4 border-gray-200 border-t-indigo-500 rounded-full animate-spin"
                aria-hidden="true"
            />
        </div>
    );
}
