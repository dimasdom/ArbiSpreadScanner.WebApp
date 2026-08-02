import OrderBlock from "./OrderBlockComponent";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from "@mui/material";
import { Trans, useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import GuideModal from '../../components/GuideModal';
import type { GuideStep } from '../../components/GuideModal';
import RealtimeLineChart from "./RealTimeLineChartComponent";
import { getFundingDirectionText } from '../../utils/spreadUtils';
import { useSpreadPage } from './hooks/useSpreadPage';

function getSpreadGuideSteps(t: TFunction<'spreads'>): GuideStep[] {
    return [
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z" />
                </svg>
            ),
            title: t('guide.spreadPage.steps.chart.title'),
            description: t('guide.spreadPage.steps.chart.description'),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                </svg>
            ),
            title: t('guide.spreadPage.steps.badges.title'),
            description: (
                <span>
                    <strong>{t('guide.spreadPage.steps.badges.spreadLabel')}</strong> — {t('guide.spreadPage.steps.badges.spreadDescription')}<br />
                    <strong>{t('guide.spreadPage.steps.badges.volatilityLabel')}</strong> — {t('guide.spreadPage.steps.badges.volatilityDescription')}
                </span>
            ),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                </svg>
            ),
            title: t('guide.spreadPage.steps.exchanges.title'),
            description: (
                <span>
                    <strong>{t('guide.spreadPage.steps.exchanges.shortLabel')}</strong> — {t('guide.spreadPage.steps.exchanges.shortDescription')}<br />
                    <strong>{t('guide.spreadPage.steps.exchanges.longLabel')}</strong> — {t('guide.spreadPage.steps.exchanges.longDescription')}<br />
                    <Trans i18nKey="guide.spreadPage.steps.exchanges.fundingNote" ns="spreads" components={{ em: <em /> }} />
                </span>
            ),
        },
        {
            icon: (
                <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
                </svg>
            ),
            title: t('guide.spreadPage.steps.orderBook.title'),
            description: t('guide.spreadPage.steps.orderBook.description'),
        },
    ];
}

// getFundingDirectionText (outside this feature's ownership, in utils/spreadUtils.ts) returns
// hardcoded English 'You pay' / 'You receive'. We map its result to a translation key here
// rather than editing that shared util.
function fundingDirectionKey(fundingRate: number, isLong: boolean): string {
    return getFundingDirectionText(fundingRate, isLong) === 'You pay'
        ? 'spreadPage.fundingDirection.pay'
        : 'spreadPage.fundingDirection.receive';
}

function SpreadPage() {
    const { t } = useTranslation('spreads');
    const {
        possiblePositionDTO,
        tickers,
        isLoading,
        isSpotSpread,
        displayedShortRate,
        displayedLongRate,
        spreadVal,
        isSpreadClosedDialogOpen,
        displayedVolatility,
        pos,
        asksA,
        bidsA,
        asksB,
        bidsB,
        totalSlippage,
        positionSize,
        spreadLabel,
        spreadClass,
        handleSpreadClosedConfirm,
    } = useSpreadPage();

    if (isLoading) {
        return (
            <div className="flex items-center justify-center min-h-64 mt-6">
                <div className="w-10 h-10 border-4 border-gray-200 dark:border-gray-700 border-t-indigo-500 rounded-full animate-spin" />
            </div>
        );
    }

    return (
        <div className="max-w-7xl mx-auto mt-6 px-4 sm:px-6 lg:px-8 pb-12">
            <GuideModal storageKey="guide_spread_seen" title={t('guide.spreadPage.title')} steps={getSpreadGuideSteps(t)} />
            <div className="shadow-2xl rounded-4xl bg-white dark:bg-gray-900 transition-colors duration-200">
                <Dialog
                    open={isSpreadClosedDialogOpen}
                    onClose={handleSpreadClosedConfirm}
                    maxWidth="xs"
                    fullWidth
                >
                    <DialogTitle className="text-lg font-semibold text-gray-900 dark:text-gray-100">
                        {t('spreadPage.closedDialog.title')}
                    </DialogTitle>
                    <DialogContent>
                        <p className="text-sm text-gray-600 dark:text-gray-400">
                            {t('spreadPage.closedDialog.message')}
                        </p>
                    </DialogContent>
                    <DialogActions className="px-6 pb-4">
                        <Button variant="contained" onClick={handleSpreadClosedConfirm}>
                            {t('spreadPage.closedDialog.ok')}
                        </Button>
                    </DialogActions>
                </Dialog>

                <div className="shadow-inner pb-4 px-4 sm:px-6 rounded-4xl lg:px-8 w-full">
                    <div className="flex justify-center p-5 rounded-2xl">
                        <RealtimeLineChart ticker={tickers} title={possiblePositionDTO?.positionModel.symbol} />
                    </div>

                    <div className="px-3 pb-2">
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 items-start">
                            <div className="md:col-span-2">
                                <h2 className="text-2xl font-semibold dark:text-gray-100">{possiblePositionDTO?.positionModel.symbol}</h2>

                                <div className="flex flex-wrap gap-2 mt-2">
                                    <span className={`${spreadClass} inline-flex items-center text-sm font-medium px-3 py-1 rounded-full`}>{spreadLabel ? t(`spreadType.${spreadLabel}`) : spreadLabel}</span>
                                    <span className="inline-flex items-center text-sm font-medium px-3 py-1 rounded-full bg-gray-100 dark:bg-gray-700 text-gray-700 dark:text-gray-300">{t('spreadPage.badges.volatility', { value: displayedVolatility?.toFixed(2) })}</span>
                                    <span className={`inline-flex items-center text-sm font-medium px-3 py-1 rounded-full ${spreadVal > 0 ? 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400' : 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'}`}>{t('spreadPage.badges.spread', { value: spreadVal.toFixed(2) })}</span>
                                </div>

                                <p className="text-sm text-gray-500 dark:text-gray-400 mt-3">{t('spreadPage.details.startSpread', { value: possiblePositionDTO?.positionModel.startSpread.toFixed(2) })}</p>
                                <p className="text-sm text-gray-500 dark:text-gray-400">{t('spreadPage.details.summaryTariff', { value: possiblePositionDTO?.positionModel.summaryTarrif.toFixed(4) })}</p>
                                <p className="text-base font-bold mt-2 dark:text-gray-100">{t('spreadPage.details.estimatedProfit', { value: possiblePositionDTO?.positionModel.possibleProfit.toFixed(2) })}</p>
                                {totalSlippage != null && (
                                    <div className="mt-2 flex flex-wrap gap-2">
                                        <span className={`inline-flex items-center text-sm font-medium px-3 py-1 rounded-full ${totalSlippage > Math.abs(spreadVal) ? 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400' : 'bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-400'}`}>
                                            {t('spreadPage.details.estSlippage', { value: totalSlippage.toFixed(3) })}
                                        </span>
                                        {positionSize > 0 && (
                                            <span className="inline-flex items-center text-sm font-medium px-3 py-1 rounded-full bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-400">
                                                {t('spreadPage.details.forPosition', { amount: positionSize.toLocaleString() })}
                                            </span>
                                        )}
                                    </div>
                                )}
                            </div>

                            <div className="md:col-span-1">
                                <p className="text-xs text-gray-500 dark:text-gray-400 mb-2">
                                    {t('spreadPage.tip')}
                                </p>
                                <div>
                                    <h3 className="text-sm font-medium text-gray-600 dark:text-gray-400">{t('spreadPage.shortExchange')}</h3>
                                    <p className="text-gray-800 dark:text-gray-200">
                                        {possiblePositionDTO?.shortExchangeUrl ? (
                                            <a
                                                href={possiblePositionDTO.shortExchangeUrl}
                                                target="_blank"
                                                rel="noopener noreferrer"
                                                className="text-indigo-600 dark:text-indigo-400 hover:underline"
                                            >
                                                {possiblePositionDTO.positionModel.exchangeShort.exchange}
                                            </a>
                                        ) : (
                                            possiblePositionDTO?.positionModel.exchangeShort.exchange
                                        )}
                                        {': '}{displayedShortRate}
                                    </p>
                                    {possiblePositionDTO?.positionModel.exchangeShort.fundingRateValue != null && possiblePositionDTO.positionModel.exchangeShort.fundingRateValue !== 0 && (
                                        <p className="text-sm text-gray-500 dark:text-gray-400">
                                            {t('spreadPage.fundingLine', {
                                                value: (possiblePositionDTO.positionModel.exchangeShort.fundingRateValue * 100).toFixed(2),
                                                direction: t(fundingDirectionKey(possiblePositionDTO.positionModel.exchangeShort.fundingRateValue, false)),
                                            })}
                                        </p>
                                    )}
                                </div>
                                <hr className="my-3 border-gray-200 dark:border-gray-700" />
                                <div>
                                    <h3 className="text-sm font-medium text-gray-600 dark:text-gray-400">{t('spreadPage.longExchange')}</h3>
                                    <p className="text-gray-800 dark:text-gray-200">
                                        {possiblePositionDTO?.longExchangeUrl ? (
                                            <a
                                                href={possiblePositionDTO.longExchangeUrl}
                                                target="_blank"
                                                rel="noopener noreferrer"
                                                className="text-indigo-600 dark:text-indigo-400 hover:underline"
                                            >
                                                {possiblePositionDTO.positionModel.exchangeLong.exchange}
                                            </a>
                                        ) : (
                                            possiblePositionDTO?.positionModel.exchangeLong.exchange
                                        )}
                                        {': '}{displayedLongRate}
                                    </p>
                                    {!isSpotSpread && possiblePositionDTO?.positionModel.exchangeLong.fundingRateValue != null && possiblePositionDTO.positionModel.exchangeLong.fundingRateValue !== 0 && (
                                        <p className="text-sm text-gray-500 dark:text-gray-400">
                                            {t('spreadPage.fundingLine', {
                                                value: (possiblePositionDTO.positionModel.exchangeLong.fundingRateValue * 100).toFixed(2),
                                                direction: t(fundingDirectionKey(possiblePositionDTO.positionModel.exchangeLong.fundingRateValue, true)),
                                            })}
                                        </p>
                                    )}
                                </div>
                            </div>
                        </div>
                    </div>

                    {(() => {
                        const hasOrderData = asksA != null && bidsA != null && asksB != null && bidsB != null;
                        return hasOrderData ? (
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
                                <div>
                                    <OrderBlock title={pos?.exchangeRateA.exchange} asks={asksA} bids={bidsA} />
                                </div>
                                <div>
                                    <OrderBlock title={pos?.exchangeRateB.exchange} asks={asksB} bids={bidsB} />
                                </div>
                            </div>
                        ) : null;
                    })()}
                </div>
            </div>
        </div>
    );
}

export default SpreadPage;
