import '@testing-library/jest-dom';
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

// Synchronous, resource-less init (no HttpBackend/Suspense) so components
// that call useTranslation() render immediately in tests instead of
// suspending on a network fetch that jsdom can't serve. Untranslated
// strings simply render as their key, which is fine since no test in
// this suite asserts on translated copy.
void i18n.use(initReactI18next).init({
    lng: 'en',
    fallbackLng: 'en',
    ns: ['common'],
    defaultNS: 'common',
    resources: { en: { common: {} } },
    interpolation: { escapeValue: false },
    react: { useSuspense: false },
});
