import { useTranslation } from 'react-i18next';
import RealtimeLineChart from '../Spread/RealTimeLineChartComponent';
import OrderBlock from '../Spread/OrderBlockComponent';
import { useLiveDemoWidget } from './hooks/useLiveDemoWidget';

function LiveDemoWidget() {
    const { t } = useTranslation('main');
    const { tickers, symbol, exchangeA, exchangeB, spreadVal, asksA, bidsA, asksB, bidsB } = useLiveDemoWidget();

    return (
        <div className="bg-gray-50 dark:bg-gray-800 rounded-2xl p-6 md:p-8 border border-gray-100 dark:border-gray-700">
            <div className="flex flex-wrap items-center justify-between gap-3 mb-2">
                <h2 className="text-2xl font-bold text-gray-900 dark:text-gray-100">{t('liveDemo.title')}</h2>
                <span className="inline-flex items-center text-sm font-medium px-3 py-1 rounded-full bg-amber-100 dark:bg-amber-900/30 text-amber-800 dark:text-amber-300">
                    <span className="w-2 h-2 rounded-full bg-amber-500 animate-pulse mr-1.5" />
                    {t('liveDemo.badge')}
                </span>
            </div>
            <p className="text-gray-600 dark:text-gray-400 mb-6">
                {t('liveDemo.description')}
            </p>

            <div className="flex justify-center mb-4">
                <RealtimeLineChart ticker={tickers} title={symbol} />
            </div>

            <div className="flex flex-wrap gap-2 mb-4">
                <span className={`inline-flex items-center text-sm font-medium px-3 py-1 rounded-full ${spreadVal > 0 ? 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400' : 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'}`}>
                    {t('liveDemo.spreadLabel', { value: spreadVal.toFixed(2) })}
                </span>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <OrderBlock title={exchangeA} asks={asksA} bids={bidsA} />
                <OrderBlock title={exchangeB} asks={asksB} bids={bidsB} />
            </div>
        </div>
    );
}

export default LiveDemoWidget;
