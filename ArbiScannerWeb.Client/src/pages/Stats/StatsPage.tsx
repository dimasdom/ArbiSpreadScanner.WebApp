import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { SpreadType } from '../../types/SpreadType';
import BarStatChart from './components/BarStatChart';
import DonutStatChart from './components/DonutStatChart';
import StatsChartCarousel, { type StatsChartPanel } from './components/StatsChartCarousel';
import { useStatsPage } from './hooks/useStatsPage';

// DDMMYYHH, in the viewer's local time zone (generatedAtUtc is UTC on the wire).
function formatSnapshotOption(generatedAtUtc: string): string {
    const date = new Date(generatedAtUtc);
    const dd = String(date.getDate()).padStart(2, '0');
    const mm = String(date.getMonth() + 1).padStart(2, '0');
    const yy = String(date.getFullYear() % 100).padStart(2, '0');
    const hh = String(date.getHours()).padStart(2, '0');
    return `${dd}${mm}${yy}${hh}`;
}

function StatsPage() {
    const { t, i18n } = useTranslation('stats');
    const {
        snapshot,
        isLoading,
        isError,
        hasAnySnapshot,
        snapshots,
        selectedSnapshotId,
        isViewingLatest,
        handleSnapshotChange,
    } = useStatsPage();

    const panels: StatsChartPanel[] = useMemo(() => {
        if (!snapshot) return [];

        return [
            {
                key: 'topSymbolsByAverageSpread',
                title: t('charts.topSymbolsByAverageSpread.title'),
                content: (
                    <BarStatChart
                        categories={snapshot.topSymbolsByAverageSpread.map((e) => e.symbol)}
                        values={snapshot.topSymbolsByAverageSpread.map((e) => e.averageSpreadPercent)}
                        valueSuffix="%"
                        emptyMessage={t('charts.topSymbolsByAverageSpread.empty')}
                    />
                ),
            },
            {
                key: 'topExchangesByCount',
                title: t('charts.topExchangesByCount.title'),
                content: (
                    <BarStatChart
                        categories={snapshot.topExchangesByCount.map((e) => e.exchange)}
                        values={snapshot.topExchangesByCount.map((e) => e.count)}
                        emptyMessage={t('charts.topExchangesByCount.empty')}
                    />
                ),
            },
            {
                key: 'topExchangePairsByCount',
                title: t('charts.topExchangePairsByCount.title'),
                content: (
                    <BarStatChart
                        categories={snapshot.topExchangePairsByCount.map((e) => `${e.exchangeA} ↔ ${e.exchangeB}`)}
                        values={snapshot.topExchangePairsByCount.map((e) => e.count)}
                        emptyMessage={t('charts.topExchangePairsByCount.empty')}
                    />
                ),
            },
            {
                key: 'medianVolumeByExchange',
                title: t('charts.medianVolumeByExchange.title'),
                content: (
                    <BarStatChart
                        categories={snapshot.medianVolumeByExchange.map((e) => e.exchange)}
                        values={snapshot.medianVolumeByExchange.map((e) => e.medianVolume)}
                        emptyMessage={t('charts.medianVolumeByExchange.empty')}
                    />
                ),
            },
            {
                key: 'spreadTypeDistribution',
                title: t('charts.spreadTypeDistribution.title'),
                content: (
                    <DonutStatChart
                        labels={snapshot.spreadTypeDistribution.map((e) => SpreadType[e.type])}
                        series={snapshot.spreadTypeDistribution.map((e) => e.count)}
                        emptyMessage={t('charts.spreadTypeDistribution.empty')}
                    />
                ),
            },
            {
                key: 'topSymbolsByCount',
                title: t('charts.topSymbolsByCount.title'),
                content: (
                    <BarStatChart
                        categories={snapshot.topSymbolsByCount.map((e) => e.symbol)}
                        values={snapshot.topSymbolsByCount.map((e) => e.count)}
                        emptyMessage={t('charts.topSymbolsByCount.empty')}
                    />
                ),
            },
        ];
    }, [snapshot, t]);

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-64 mt-6">
                <div className="w-10 h-10 border-4 border-gray-200 dark:border-gray-700 border-t-indigo-500 rounded-full animate-spin" />
            </div>
        );
    }

    return (
        <div className="max-w-5xl mx-auto mt-6 px-4 sm:px-6 lg:px-8 pb-12">
            <div className="shadow-2xl rounded-4xl bg-white dark:bg-gray-900 transition-colors duration-200 p-4 sm:p-6 lg:p-8">
                <div className="flex flex-col sm:flex-row sm:items-end sm:justify-between gap-4 mb-6">
                    <div>
                        <h1 className="text-2xl font-semibold dark:text-gray-100">{t('page.title')}</h1>
                        <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">{t('page.subtitle')}</p>
                    </div>

                    {hasAnySnapshot && (
                        <div className="flex items-end gap-2">
                            <div className="flex flex-col">
                                <label htmlFor="stats-date" className="text-xs font-medium text-gray-600 dark:text-gray-400 mb-1">
                                    {t('dateSelector.label')}
                                </label>
                                <select
                                    id="stats-date"
                                    value={selectedSnapshotId}
                                    onChange={(e) => handleSnapshotChange(e.target.value)}
                                    className="block rounded-xl border-gray-300 dark:border-gray-600 bg-gray-50 dark:bg-gray-800 dark:text-gray-100 px-4 py-2 text-sm focus:border-indigo-500 focus:ring-indigo-500"
                                >
                                    {snapshots.map((entry) => (
                                        <option key={entry.id} value={entry.id}>
                                            {formatSnapshotOption(entry.generatedAtUtc)}
                                        </option>
                                    ))}
                                </select>
                            </div>
                            {!isViewingLatest && (
                                <button
                                    type="button"
                                    onClick={() => handleSnapshotChange('')}
                                    className="rounded-xl px-4 py-2 text-sm font-medium bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
                                >
                                    {t('dateSelector.latest')}
                                </button>
                            )}
                        </div>
                    )}
                </div>

                {isError || !snapshot ? (
                    <div className="flex items-center justify-center min-h-40 text-center text-gray-500 dark:text-gray-400 text-sm px-6">
                        {t('empty.noSnapshot')}
                    </div>
                ) : (
                    <>
                        <div className="flex flex-wrap gap-2 mb-6">
                            <span className="inline-flex items-center text-sm font-medium px-3 py-1 rounded-full bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">
                                {t('page.totalAnalyzed', { count: snapshot.totalSpreadsAnalyzed })}
                            </span>
                            <span className="inline-flex items-center text-sm font-medium px-3 py-1 rounded-full bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">
                                {t('page.generatedAt', {
                                    date: new Date(snapshot.generatedAtUtc).toLocaleString(i18n.language, {
                                        dateStyle: 'medium',
                                        timeStyle: 'short',
                                    }),
                                })}
                            </span>
                        </div>

                        <StatsChartCarousel panels={panels} />
                    </>
                )}
            </div>
        </div>
    );
}

export default StatsPage;
