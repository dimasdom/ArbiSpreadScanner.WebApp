import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import HttpBackend from 'i18next-http-backend';
import store from '../store/store';
import { setLanguage } from '../store/slices/languageSlice';
import { DEFAULT_LANG, SUPPORTED_LANG_CODES } from './languages';
import { PREFERRED_LANG_KEY, getPreferredLanguage } from './detect';

// All namespaces that exist, for reference — NOT passed to init()'s `ns`
// option. i18next eagerly preloads every namespace listed in `ns` for
// the active language at startup, which would defeat lazy loading.
// Only `common` (needed by NavBar/Footer on every page) is preloaded;
// every other namespace is fetched on demand the first time a
// component calls useTranslation('<namespace>').
export const NAMESPACES = ['common', 'main', 'account', 'spreads', 'stats', 'subscription', 'faq', 'mcpToken', 'chat'] as const;

void i18n
    .use(HttpBackend)
    .use(initReactI18next)
    .init({
        lng: getPreferredLanguage(),
        supportedLngs: SUPPORTED_LANG_CODES,
        fallbackLng: DEFAULT_LANG,
        ns: ['common'],
        defaultNS: 'common',
        load: 'languageOnly',
        backend: { loadPath: '/locales/{{lng}}/{{ns}}.json' },
        interpolation: { escapeValue: false },
        react: { useSuspense: true },
    });

// Single place that keeps the Redux language slice and localStorage in
// sync with i18next, no matter which component triggered the change
// (LangGuard on URL parse, or the language switcher on user selection).
i18n.on('languageChanged', (lng) => {
    store.dispatch(setLanguage(lng));
    localStorage.setItem(PREFERRED_LANG_KEY, lng);
});

export default i18n;
