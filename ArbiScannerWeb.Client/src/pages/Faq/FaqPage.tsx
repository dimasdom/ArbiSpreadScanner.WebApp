import type { ReactElement } from 'react';
import { Trans, useTranslation } from 'react-i18next';
import FaqElement from './FaqElement';

interface FaqItemDef {
    key: string;
    components?: Record<string, ReactElement>;
}

const faqItemDefs: FaqItemDef[] = [
    { key: 'whatIs' },
    {
        key: 'exchanges',
        components: { bold: <span className="font-semibold text-gray-800 dark:text-gray-200" /> },
    },
    { key: 'smartAlerts' },
    { key: 'riskManagement' },
    { key: 'personalize' },
    {
        key: 'spreadThreshold',
        components: { strong: <strong /> },
    },
    {
        key: 'shortLongExchange',
        components: { strong: <strong />, br: <br /> },
    },
    {
        key: 'orderBook',
        components: { strong: <strong />, br: <br /> },
    },
    {
        key: 'volatility',
        components: { strong: <strong />, br: <br /> },
    },
    {
        key: 'fundingRate',
        components: { strong: <strong />, em: <em />, br: <br /> },
    },
];

function FaqPage() {
    const { t } = useTranslation('faq');

    return (
        <div className="max-w-7xl mx-auto mt-6 px-4 sm:px-6 lg:px-8 pb-12">
            <div className="bg-white dark:bg-gray-900 shadow-2xl rounded-3xl overflow-hidden transition-colors duration-200">
                {/* Header Section */}
                <div className="bg-linear-to-r from-blue-600 to-indigo-700 py-10 sm:py-16 px-4 sm:px-8 text-center text-white">
                    <h1 className="text-2xl sm:text-4xl md:text-6xl font-extrabold mb-4 sm:mb-6 tracking-tight">
                        {t('header.title')}
                    </h1>
                    <p className="text-base sm:text-xl md:text-2xl max-w-3xl mx-auto opacity-90 leading-relaxed">
                        <Trans
                            i18nKey="header.subtitle"
                            ns="faq"
                            components={{ brand: <span className="font-semibold text-yellow-300" /> }}
                        />
                    </p>
                </div>

                {/* FAQ Content Section */}
                <div className="p-8 md:p-12">
                    <div className="max-w-4xl mx-auto bg-white dark:bg-gray-900 transition-colors duration-200">
                        <div className="space-y-1">
                            {faqItemDefs.map((item) => (
                                <FaqElement
                                    key={item.key}
                                    question={t(`items.${item.key}.question`)}
                                    answer={
                                        item.components ? (
                                            <Trans
                                                i18nKey={`items.${item.key}.answer`}
                                                ns="faq"
                                                components={item.components}
                                            />
                                        ) : (
                                            t(`items.${item.key}.answer`)
                                        )
                                    }
                                />
                            ))}
                        </div>
                    </div>

                    {/* Contact Support CTA */}
                    <div className="mt-16 text-center border-t border-gray-100 dark:border-gray-700 pt-10">
                        <p className="text-gray-600 dark:text-gray-400 mb-4">
                            {t('contact.prompt')}
                        </p>
                        <a
                            href="mailto:arbiscannerweb@atomicmail.io"
                            className="inline-block bg-indigo-600 text-white px-8 py-3 rounded-full font-semibold shadow-lg hover:bg-indigo-700 transition-all transform hover:-translate-y-0.5"
                        >
                            {t('contact.button')}
                        </a>
                        <p className="text-sm text-gray-400 dark:text-gray-500 mt-3">
                            <Trans
                                i18nKey="contact.emailNote"
                                ns="faq"
                                components={{ email: <span className="text-indigo-600 dark:text-indigo-400 font-medium" /> }}
                            />
                        </p>
                    </div>

                    {/* Reset guides */}
                    <div className="mt-8 text-center">
                        <button type="button"
                            onClick={() => {
                                localStorage.removeItem('guides-all-disabled');
                                const keys = Object.keys(localStorage).filter(k => k !== 'guides-all-disabled');
                                keys.forEach(k => {
                                    if (localStorage.getItem(k) === 'true') localStorage.removeItem(k);
                                });
                                globalThis.location.reload();
                            }}
                            className="inline-flex items-center gap-2 text-sm bg-indigo-600 text-white hover:bg-indigo-700 transition-colors rounded-full px-4 py-2"
                        >
                            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                            </svg>
                            {t('guides.seeAgain')}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default FaqPage;
