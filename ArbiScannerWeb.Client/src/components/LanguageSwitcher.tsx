import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { useAppSelector } from '../hooks';
import { SUPPORTED_LANGUAGES, getLanguageMeta } from '../i18n/languages';
import { stripLangPrefix, withLang } from '../i18n/routing';

interface LanguageSwitcherProps {
    onSelect?: () => void;
}

export default function LanguageSwitcher({ onSelect }: LanguageSwitcherProps) {
    const { t } = useTranslation('common');
    const [open, setOpen] = useState(false);
    const location = useLocation();
    // Plain (non-localized) navigate: the target path below is already
    // built with the NEW language, so useLocalizedNavigate would
    // double-prefix it with the still-current language.
    const navigate = useNavigate();
    const currentLang = useAppSelector((state) => state.language.language);
    const current = getLanguageMeta(currentLang);

    const select = (code: string) => {
        setOpen(false);
        onSelect?.();
        // LangGuard picks up the new :lang param and calls
        // i18n.changeLanguage(), which is what actually updates the
        // language slice and localStorage (single source of truth).
        navigate(withLang(stripLangPrefix(location.pathname), code) + location.search + location.hash);
    };

    return (
        <div className="relative">
            <button
                onClick={() => setOpen((o) => !o)}
                className="p-2 rounded-md bg-white dark:bg-gray-800 shadow-sm hover:shadow-md transition-shadow text-sm text-gray-700 dark:text-gray-300 flex items-center gap-1"
                aria-label={t('language.switchAria')}
                aria-haspopup="true"
                aria-expanded={open}
            >
                <span className="uppercase font-medium">{current.code}</span>
                <span aria-hidden="true">{current.flag}</span>
            </button>

            {open && (
                <>
                    <div className="fixed inset-0 z-40" onClick={() => setOpen(false)} aria-hidden="true" />
                    <div className="absolute right-0 mt-2 w-32 bg-white dark:bg-gray-800 rounded-md shadow-lg z-50 py-1 border border-gray-100 dark:border-gray-700">
                        {SUPPORTED_LANGUAGES.map((l) => (
                            <button
                                key={l.code}
                                onClick={() => select(l.code)}
                                className={`w-full text-left px-3 py-2 text-sm hover:bg-gray-50 dark:hover:bg-gray-700 flex items-center gap-2 ${l.code === current.code ? 'font-semibold text-indigo-600 dark:text-indigo-400' : 'text-gray-700 dark:text-gray-300'
                                    }`}
                            >
                                <span className="uppercase">{l.code}</span>
                                <span aria-hidden="true">{l.flag}</span>
                            </button>
                        ))}
                    </div>
                </>
            )}
        </div>
    );
}
