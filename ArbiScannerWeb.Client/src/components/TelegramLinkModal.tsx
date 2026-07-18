import React from 'react';
import TelegramIcon from '@mui/icons-material/Telegram';
import { useTranslation } from 'react-i18next';

interface TelegramLinkModalProps {
  isOpen: boolean;
  linkRequestId: string;
  onClose: () => void;
}

const TelegramLinkModal: React.FC<TelegramLinkModalProps> = ({ isOpen, linkRequestId, onClose }) => {
  const { t } = useTranslation('common');
  if (!isOpen) return null;

  const handleLinkTelegram = () => {
    const botUsername = 'futures_and_other_arbitrage_bot';
    const telegramUrl = `https://t.me/${botUsername}?text=${linkRequestId}`;

    globalThis.open(telegramUrl, '_blank');
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-50">
      <div className="bg-white dark:bg-gray-900 rounded-2xl shadow-xl max-w-lg w-full mx-4 overflow-hidden flex flex-col">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-12 h-12 bg-blue-100 dark:bg-blue-900/40 rounded-full flex items-center justify-center">
                <TelegramIcon sx={{ fontSize: 28, color: '#0088cc' }} />
              </div>
              <h2 className="text-2xl font-bold text-gray-800 dark:text-gray-100">{t('telegramLink.title')}</h2>
            </div>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
              aria-label={t('actions.close')}
            >
              <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>

        <div className="p-6">
          <div className="space-y-4 text-gray-700 dark:text-gray-300">
            <p className="text-lg">
              {t('telegramLink.intro')}
            </p>

            <div className="bg-blue-50 dark:bg-blue-900/20 rounded-lg p-4 border border-blue-100 dark:border-blue-800">
              <h3 className="font-semibold text-gray-900 dark:text-gray-100 mb-2">{t('telegramLink.benefitsHeading')}</h3>
              <ul className="space-y-2 text-sm">
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 dark:text-blue-400 mt-0.5">✓</span>
                  <span>{t('telegramLink.benefits.realTime')}</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 dark:text-blue-400 mt-0.5">✓</span>
                  <span>{t('telegramLink.benefits.customized')}</span>
                </li>
                <li className="flex items-start gap-2">
                  <span className="text-blue-600 dark:text-blue-400 mt-0.5">✓</span>
                  <span>{t('telegramLink.benefits.directAccess')}</span>
                </li>
              </ul>
            </div>

            <p className="text-sm text-gray-600 dark:text-gray-400">
              {t('telegramLink.instructions')}
            </p>
          </div>
        </div>

        <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex justify-end gap-3">
          <button
            onClick={onClose}
            className="px-6 py-2.5 rounded-lg bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 font-medium hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
          >
            {t('actions.cancel')}
          </button>
          <button
            onClick={handleLinkTelegram}
            className="px-6 py-2.5 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 transition-colors shadow-sm flex items-center gap-2"
          >
            <TelegramIcon sx={{ fontSize: 20 }} />
            {t('telegramLink.openTelegram')}
          </button>
        </div>
      </div>
    </div>
  );
};

export default TelegramLinkModal;
