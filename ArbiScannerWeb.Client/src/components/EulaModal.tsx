import React from 'react';
import { useTranslation } from 'react-i18next';

interface EulaModalProps {
  isOpen: boolean;
  onAgree: () => void;
  onCancel: () => void;
}

const EULA_SECTION_KEYS = ['acceptance', 'useOfService', 'financialDisclaimer', 'dataPrivacy', 'accountResponsibilities', 'termination'] as const;

const EulaModal: React.FC<EulaModalProps> = ({ isOpen, onAgree, onCancel }) => {
  const { t } = useTranslation('common');
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center backdrop-blur-sm bg-white/30 dark:bg-black/50">
      <div className="bg-white dark:bg-gray-900 rounded-2xl shadow-xl max-w-2xl w-full mx-4 max-h-[80vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-2xl font-bold text-gray-800 dark:text-gray-100">{t('eula.title')}</h2>
        </div>
        <div className="p-6 overflow-y-auto flex-1">
          <div className="prose prose-sm max-w-none text-gray-700 dark:text-gray-300 space-y-4">
            <p className="font-semibold">{t('eula.intro')}</p>
            {EULA_SECTION_KEYS.map((key, index) => (
              <React.Fragment key={key}>
                <h3 className="text-lg font-semibold mt-4 text-gray-900 dark:text-gray-100">{index + 1}. {t(`eula.sections.${key}.heading`)}</h3>
                <p>{t(`eula.sections.${key}.body`)}</p>
              </React.Fragment>
            ))}
          </div>
        </div>
        <div className="p-6 border-t border-gray-200 dark:border-gray-700 flex justify-end gap-3">
          <button type="button"
            onClick={onCancel}
            className="px-6 py-2 rounded-lg bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 font-medium hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
          >
            {t('actions.cancel')}
          </button>
          <button type="button"
            onClick={onAgree}
            className="px-6 py-2 rounded-lg bg-indigo-600 text-white font-medium hover:bg-indigo-700 transition-colors"
          >
            {t('eula.agree')}
          </button>
        </div>
      </div>
    </div>
  );
};

export default EulaModal;
