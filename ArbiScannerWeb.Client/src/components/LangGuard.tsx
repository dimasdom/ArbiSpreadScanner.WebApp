import { useEffect } from 'react';
import { Navigate, Outlet, useLocation, useParams } from 'react-router';
import { useTranslation } from 'react-i18next';
import { isSupportedLang } from '../i18n/languages';
import { getPreferredLanguage } from '../i18n/detect';

// Validates the :lang route param. Known route segments never collide
// with a supported 2-letter language code today (account, spreads,
// spread, subscriptions, payment, faq, login, register, ...), so an
// invalid/missing :lang always means "no language segment was present".
export function LangGuard() {
    const { lang } = useParams<{ lang: string }>();
    const location = useLocation();
    const { i18n } = useTranslation();

    useEffect(() => {
        if (isSupportedLang(lang) && i18n.language !== lang) {
            void i18n.changeLanguage(lang);
        }
    }, [lang, i18n]);

    if (!isSupportedLang(lang)) {
        const detected = getPreferredLanguage();
        return <Navigate to={`/${detected}${location.pathname}${location.search}${location.hash}`} replace />;
    }

    return <Outlet />;
}

export function RootRedirect() {
    return <Navigate to={`/${getPreferredLanguage()}/`} replace />;
}
