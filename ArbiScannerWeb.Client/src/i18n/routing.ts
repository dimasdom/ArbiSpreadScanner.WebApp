import { useCallback } from 'react';
import { useNavigate, type NavigateOptions, type To } from 'react-router';
import { useTranslation } from 'react-i18next';
import { isSupportedLang } from './languages';

// Prepends the lang segment to absolute paths only; relative paths and
// query/hash-only strings pass through unchanged.
export function withLang(path: string, lang: string): string {
    if (!path.startsWith('/')) return path;
    return `/${lang}${path}`;
}

function withLangTo(to: To, lang: string): To {
    if (typeof to === 'string') return withLang(to, lang);
    return to.pathname ? { ...to, pathname: withLang(to.pathname, lang) } : to;
}

// Strips a leading /<lang> segment so page-transition keys and other
// path comparisons ignore the language, e.g. "/uk/faq" -> "/faq".
export function stripLangPrefix(pathname: string): string {
    const match = pathname.match(/^\/([a-zA-Z]{2})(?=\/|$)/);
    if (match && isSupportedLang(match[1])) {
        return pathname.slice(match[0].length) || '/';
    }
    return pathname;
}

// Drop-in replacement for react-router's useNavigate() that keeps the
// current language segment on every internal, absolute navigation.
export function useLocalizedNavigate() {
    const navigate = useNavigate();
    const { i18n } = useTranslation();

    return useCallback(
        (to: To | number, options?: NavigateOptions) => {
            if (typeof to === 'number') {
                navigate(to);
                return;
            }
            navigate(withLangTo(to, i18n.language), options);
        },
        [navigate, i18n.language]
    );
}
