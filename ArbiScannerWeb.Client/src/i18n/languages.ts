export interface LanguageMeta {
    code: string;
    label: string;
    flag: string;
}

// English pairs with the UK flag (rather than US) to keep the whole set
// consistent with "flag = country most associated with the language",
// the same logic behind pairing Russian with the UN flag below.
export const SUPPORTED_LANGUAGES: LanguageMeta[] = [
    { code: 'en', label: 'EN', flag: '🇬🇧' },
    { code: 'es', label: 'ES', flag: '🇪🇸' },
    { code: 'de', label: 'DE', flag: '🇩🇪' },
    { code: 'fr', label: 'FR', flag: '🇫🇷' },
    // UN flag used intentionally instead of the Russian flag.
    { code: 'ru', label: 'RU', flag: '🇺🇳' },
    { code: 'uk', label: 'UK', flag: '🇺🇦' },
];

export const SUPPORTED_LANG_CODES = SUPPORTED_LANGUAGES.map((l) => l.code);

export const DEFAULT_LANG = 'en';

export function isSupportedLang(code: string | undefined | null): code is string {
    return !!code && SUPPORTED_LANG_CODES.includes(code);
}

export function getLanguageMeta(code: string): LanguageMeta {
    return SUPPORTED_LANGUAGES.find((l) => l.code === code) ?? SUPPORTED_LANGUAGES[0];
}
