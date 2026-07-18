import { DEFAULT_LANG, isSupportedLang } from './languages';

export const PREFERRED_LANG_KEY = 'preferredLang';

// Order of precedence: explicit prior choice (localStorage) -> browser
// language list -> hard default. This is only used to pick the *redirect
// target* for first-visit / invalid-URL cases; once a URL has a valid
// :lang segment, that segment is always the source of truth.
export function getPreferredLanguage(): string {
    const stored = localStorage.getItem(PREFERRED_LANG_KEY);
    if (isSupportedLang(stored)) return stored;

    for (const navLang of navigator.languages ?? [navigator.language]) {
        const primary = navLang.split('-')[0].toLowerCase();
        if (isSupportedLang(primary)) return primary;
    }

    return DEFAULT_LANG;
}
