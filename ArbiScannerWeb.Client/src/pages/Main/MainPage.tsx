import { useEffect } from 'react';
import { useSelector } from 'react-redux';
import { Trans, useTranslation } from 'react-i18next';
import TuneIcon from '@mui/icons-material/Tune';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import SecurityIcon from '@mui/icons-material/Security';
import GuideModal from '../../components/GuideModal';
import type { GuideStep } from '../../components/GuideModal';
import LiveDemoWidget from './LiveDemoWidget';
import type { IRootStore } from '../../store/store';
import { useLocalizedNavigate } from '../../i18n/routing';

const guideStepIcons = [
    (
        <svg key="welcome" className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
        </svg>
    ),
    (
        <svg key="spreadTypes" className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z" />
        </svg>
    ),
    (
        <svg key="smartAlerts" className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
        </svg>
    ),
    (
        <svg key="getStarted" className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
        </svg>
    ),
];

const exchanges = [
    'Binance', 'Bybit', 'OKX', 'KuCoin Futures', 'MEXC', 'Bitget',
    'HTX', 'XT', 'CoinEX', 'LBank', 'WhiteBit', 'Gate.io', 'BingX'
];

function MainPage() {
    const { t } = useTranslation('main');
    const navigate = useLocalizedNavigate();
    const isLoggedIn = useSelector((state: IRootStore) => state.account.isLoggedIn);
    const sessionChecked = useSelector((state: IRootStore) => state.account.sessionChecked);

    useEffect(() => {
        if (!sessionChecked || !isLoggedIn) return;
        if (sessionStorage.getItem('redirected_to_spreads_this_session')) return;
        sessionStorage.setItem('redirected_to_spreads_this_session', 'true');
        navigate('/spreads');
    }, [sessionChecked, isLoggedIn, navigate]);

    const mainGuideSteps: GuideStep[] = [
        {
            icon: guideStepIcons[0],
            title: t('guide.steps.welcome.title'),
            description: t('guide.steps.welcome.description'),
        },
        {
            icon: guideStepIcons[1],
            title: t('guide.steps.spreadTypes.title'),
            description: (
                <span>
                    <Trans
                        i18nKey="guide.steps.spreadTypes.description"
                        ns="main"
                        components={{ strong: <strong />, br: <br /> }}
                    />
                </span>
            ),
        },
        {
            icon: guideStepIcons[2],
            title: t('guide.steps.smartAlerts.title'),
            description: t('guide.steps.smartAlerts.description'),
        },
        {
            icon: guideStepIcons[3],
            title: t('guide.steps.getStarted.title'),
            description: t('guide.steps.getStarted.description'),
        },
    ];

    return (
        <div className="max-w-7xl mx-auto mt-6 px-4 sm:px-6 lg:px-8 pb-12">
            <GuideModal storageKey="guide_main_seen" title={t('guide.title')} steps={mainGuideSteps} />
            <div className="bg-white dark:bg-gray-900 shadow-2xl rounded-3xl overflow-hidden transition-colors duration-200">
                <div className="bg-linear-to-r from-blue-600 to-indigo-700 py-16 px-8 text-center text-white">
                    <h1 className="text-4xl md:text-6xl font-extrabold mb-6 tracking-tight">
                        {t('hero.title')}
                    </h1>
                    <p className="text-xl md:text-2xl max-w-3xl mx-auto opacity-90 leading-relaxed">
                        <Trans
                            i18nKey="hero.subtitle"
                            ns="main"
                            components={{ highlight: <span className="font-semibold text-yellow-300" /> }}
                        />
                    </p>
                </div>

                <div className="p-8 md:p-12">
                    {sessionChecked && !isLoggedIn && (
                        <div className="mb-16">
                            <LiveDemoWidget />
                        </div>
                    )}

                    <div className="mb-16 text-center">
                        <h2 className="text-3xl font-bold text-gray-800 dark:text-gray-100 mb-8">{t('exchanges.heading')}</h2>
                        <div className="flex flex-wrap justify-center gap-4">
                            {exchanges.map((ex) => (
                                <span key={ex} className="px-5 py-2 rounded-full bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 font-semibold shadow-sm border border-gray-200 dark:border-gray-600 hover:bg-indigo-50 dark:hover:bg-indigo-900 hover:text-indigo-700 dark:hover:text-indigo-300 hover:border-indigo-200 dark:hover:border-indigo-700 transition-colors duration-200 cursor-default">
                                    {ex}
                                </span>
                            ))}
                        </div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
                        <div className="bg-gray-50 dark:bg-gray-800 rounded-2xl p-8 hover:shadow-lg transition-shadow duration-300 border border-gray-100 dark:border-gray-700 text-center">
                            <div className="w-16 h-16 bg-indigo-100 dark:bg-indigo-900 rounded-full flex items-center justify-center mx-auto mb-6 text-indigo-600 dark:text-indigo-400">
                                <TuneIcon fontSize="large" />
                            </div>
                            <h3 className="text-2xl font-bold text-gray-900 dark:text-gray-100 mb-4">{t('features.personalize.title')}</h3>
                            <p className="text-gray-600 dark:text-gray-400">
                                {t('features.personalize.description')}
                            </p>
                        </div>

                        <div className="bg-gray-50 dark:bg-gray-800 rounded-2xl p-8 hover:shadow-lg transition-shadow duration-300 border border-gray-100 dark:border-gray-700 text-center">
                            <div className="w-16 h-16 bg-blue-100 dark:bg-blue-900 rounded-full flex items-center justify-center mx-auto mb-6 text-blue-600 dark:text-blue-400">
                                <SecurityIcon fontSize="large" />
                            </div>
                            <h3 className="text-2xl font-bold text-gray-900 dark:text-gray-100 mb-4">{t('features.riskManagement.title')}</h3>
                            <p className="text-gray-600 dark:text-gray-400">
                                {t('features.riskManagement.description')}
                            </p>
                        </div>

                        <div className="bg-gray-50 dark:bg-gray-800 rounded-2xl p-8 hover:shadow-lg transition-shadow duration-300 border border-gray-100 dark:border-gray-700 text-center">
                            <div className="w-16 h-16 bg-green-100 dark:bg-green-900 rounded-full flex items-center justify-center mx-auto mb-6 text-green-600 dark:text-green-400">
                                <NotificationsActiveIcon fontSize="large" />
                            </div>
                            <h3 className="text-2xl font-bold text-gray-900 dark:text-gray-100 mb-4">{t('features.smartAlerts.title')}</h3>
                            <p className="text-gray-600 dark:text-gray-400">
                                {t('features.smartAlerts.description')}
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default MainPage;
