import TuneIcon from '@mui/icons-material/Tune';
import NotificationsActiveIcon from '@mui/icons-material/NotificationsActive';
import SecurityIcon from '@mui/icons-material/Security';
import GuideModal from '../../components/GuideModal';
import type { GuideStep } from '../../components/GuideModal';

const mainGuideSteps: GuideStep[] = [
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
            </svg>
        ),
        title: 'Welcome to ArbiScanner',
        description: 'ArbiScanner monitors price differences across multiple exchanges in real time, helping you spot profitable spread trading opportunities — including funding, spot, and futures spreads.',
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z" />
            </svg>
        ),
        title: 'Three Spread Types',
        description: (
            <span>
                <strong>Futures</strong> — price gap between futures contracts on two exchanges.<br />
                <strong>Funding</strong> — perpetual funding rate arbitrage between longs and shorts.<br />
                <strong>Spot</strong> — price difference for the same asset on two spot markets.
            </span>
        ),
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
            </svg>
        ),
        title: 'Smart Alerts',
        description: 'Set your minimum spread threshold in Account Settings. You will get notified instantly — via dashboard and Telegram — whenever a spread exceeds your target.',
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
        ),
        title: 'Get Started',
        description: 'Register an account, subscribe to a plan, then configure your preferences in Account Settings: choose your exchanges, set your spread size threshold, and connect Telegram for instant alerts.',
    },
];

const exchanges = [
    'Binance', 'Bybit', 'OKX', 'KuCoin Futures', 'MEXC', 'Bitget', 
    'HTX', 'XT', 'CoinEX', 'LBank', 'WhiteBit', 'Gate.io', 'BingX'
];

function MainPage() {
    return (
        <div className="max-w-7xl mx-auto mt-6 px-4 sm:px-6 lg:px-8 pb-12">
            <GuideModal storageKey="guide_main_seen" title="How ArbiScanner Works" steps={mainGuideSteps} />
            <div className="bg-white shadow-2xl rounded-3xl overflow-hidden">
                <div className="bg-gradient-to-r from-blue-600 to-indigo-700 py-16 px-8 text-center text-white">
                    <h1 className="text-4xl md:text-6xl font-extrabold mb-6 tracking-tight">
                        Welcome to ArbiScanner
                    </h1>
                    <p className="text-xl md:text-2xl max-w-3xl mx-auto opacity-90 leading-relaxed">
                        The ultimate web app to trade different types of spreads including 
                        <span className="font-semibold text-yellow-300"> funding</span>, 
                        <span className="font-semibold text-yellow-300"> spot</span>, and 
                        <span className="font-semibold text-yellow-300"> futures spreads</span>.
                    </p>
                </div>

                <div className="p-8 md:p-12">
                    <div className="mb-16 text-center">
                        <h2 className="text-3xl font-bold text-gray-800 mb-8">Supported Exchanges</h2>
                        <div className="flex flex-wrap justify-center gap-4">
                            {exchanges.map((ex) => (
                                <span key={ex} className="px-5 py-2 rounded-full bg-gray-100 text-gray-700 font-semibold shadow-sm border border-gray-200 hover:bg-indigo-50 hover:text-indigo-700 hover:border-indigo-200 transition-colors duration-200 cursor-default">
                                    {ex}
                                </span>
                            ))}
                        </div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
                        <div className="bg-gray-50 rounded-2xl p-8 hover:shadow-lg transition-shadow duration-300 border border-gray-100 text-center">
                            <div className="w-16 h-16 bg-indigo-100 rounded-full flex items-center justify-center mx-auto mb-6 text-indigo-600">
                                <TuneIcon fontSize="large" />
                            </div>
                            <h3 className="text-2xl font-bold text-gray-900 mb-4">Personalize Experience</h3>
                            <p className="text-gray-600">
                                Tailor your dashboard by selecting only the exchanges you like and trust. Focus on what matters to you.
                            </p>
                        </div>

                        <div className="bg-gray-50 rounded-2xl p-8 hover:shadow-lg transition-shadow duration-300 border border-gray-100 text-center">
                            <div className="w-16 h-16 bg-blue-100 rounded-full flex items-center justify-center mx-auto mb-6 text-blue-600">
                                <SecurityIcon fontSize="large" />
                            </div>
                            <h3 className="text-2xl font-bold text-gray-900 mb-4">Risk Management</h3>
                            <p className="text-gray-600">
                                Define your custom position size to match your portfolio and risk tolerance automatically.
                            </p>
                        </div>

                        <div className="bg-gray-50 rounded-2xl p-8 hover:shadow-lg transition-shadow duration-300 border border-gray-100 text-center">
                            <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-6 text-green-600">
                                <NotificationsActiveIcon fontSize="large" />
                            </div>
                            <h3 className="text-2xl font-bold text-gray-900 mb-4">Smart Alerts</h3>
                            <p className="text-gray-600">
                                Set your minimum spread thresholds to get notified instantly when profitable opportunities arise.
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}

export default MainPage;