import React, { useState, useEffect } from 'react';
import { Trans, useTranslation } from 'react-i18next';

const COOKIE_CONSENT_KEY = 'cookie_consent_accepted';

const CookieConsentModal: React.FC = () => {
  const { t } = useTranslation('common');
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    const accepted = localStorage.getItem(COOKIE_CONSENT_KEY);
    if (!accepted) {
      setIsVisible(true);
    }
  }, []);

  const handleAccept = () => {
    localStorage.setItem(COOKIE_CONSENT_KEY, 'true');
    setIsVisible(false);
  };

  if (!isVisible) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center backdrop-blur-sm bg-black/40">
      <div className="bg-white dark:bg-gray-900 rounded-2xl shadow-xl max-w-md w-full mx-4 mb-4 sm:mb-0 p-6 flex flex-col gap-4">
        <div className="flex items-center gap-3">
          <span className="text-2xl">🍪</span>
          <h2 className="text-lg font-bold text-gray-800 dark:text-gray-100">{t('cookieConsent.title')}</h2>
        </div>
        <p className="text-gray-600 dark:text-gray-400 text-sm leading-relaxed">
          <Trans i18nKey="cookieConsent.body" ns="common" components={{ strong: <strong /> }} />
        </p>
        <div className="flex justify-end">
          <button
            onClick={handleAccept}
            className="px-6 py-2 rounded-lg bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors"
          >
            {t('actions.ok')}
          </button>
        </div>
      </div>
    </div>
  );
};

export default CookieConsentModal;
