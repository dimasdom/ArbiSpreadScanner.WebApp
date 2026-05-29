import OrderBlock from "./OrderBlockComponent";
import { Button, Dialog, DialogActions, DialogContent, DialogTitle } from "@mui/material";
import GuideModal from '../../components/GuideModal';
import type { GuideStep } from '../../components/GuideModal';
import RealtimeLineChart from "./RealTimeLineChartComponent";
import { getFundingDirectionText } from '../../utils/spreadUtils';
import { useSpreadPage } from './hooks/useSpreadPage';

const spreadGuideSteps: GuideStep[] = [
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z" />
            </svg>
        ),
        title: 'Live Price Chart',
        description: 'The chart shows real-time price movements for both exchanges. Watch for convergence or divergence — a narrowing spread means the opportunity may be closing.',
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
        ),
        title: 'Spread & Volatility Badges',
        description: (
            <span>
                <strong>Spread %</strong> — current profit potential after tariffs. Green = positive, red = negative.<br />
                <strong>Volatility</strong> — recent price fluctuation. Higher volatility = higher risk but potentially faster close.
            </span>
        ),
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
            </svg>
        ),
        title: 'Short & Long Exchanges',
        description: (
            <span>
                <strong>Short Exchange</strong> — sell (short) the asset here where the price is higher.<br />
                <strong>Long Exchange</strong> — buy (long) the asset here where the price is lower.<br />
                For funding spreads, the direction also tells you whether you <em>pay</em> or <em>receive</em> the funding rate.
            </span>
        ),
    },
    {
        icon: (
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
        ),
        title: 'Order Book Depth',
        description: 'The order book panels show the top bids (buyers) and asks (sellers) for each exchange. Larger order sizes at your target price mean better liquidity and less slippage when entering the trade.',
    },
];

function SpreadPage() {
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
                <div className="w-10 h-10 border-4 border-gray-200 border-t-indigo-500 rounded-full animate-spin" />
            </div>
        );
    }

    return (
        <div className="max-w-5xl mx-auto mt-6 shadow-2xl rounded-4xl bg-white">
            <GuideModal storageKey="guide_spread_seen" title="Reading the Spread Detail" steps={spreadGuideSteps} />
            <Dialog
                open={isSpreadClosedDialogOpen}
                onClose={handleSpreadClosedConfirm}
                maxWidth="xs"
                fullWidth
            >
                <DialogTitle className="text-lg font-semibold text-gray-900">
                    Spread Closed
                </DialogTitle>
                <DialogContent>
                    <p className="text-sm text-gray-600">
                        This spread has been closed and is no longer available.
                    </p>
                </DialogContent>
                <DialogActions className="px-6 pb-4">
                    <Button variant="contained" onClick={handleSpreadClosedConfirm}>
                        OK
                    </Button>
                </DialogActions>
            </Dialog>

            <div className="shadow-inner pb-4 px-4 sm:px-6 rounded-4xl lg:px-8 w-full">
                <div className="flex justify-center p-5 rounded-2xl ">
                    <RealtimeLineChart ticker={tickers} title={possiblePositionDTO?.positionModel.symbol} />
                </div>

                <div className="px-3 pb-2">
                    <div className="grid grid-cols-1  md:grid-cols-3 gap-4 items-start">
                        <div className="md:col-span-2">
                            <h2 className="text-2xl font-semibold">{possiblePositionDTO?.positionModel.symbol}</h2>

                            <div className="flex flex-wrap gap-2 mt-2">
                                <span className={`${spreadClass} inline-flex items-center text-sm font-medium px-3 py-1 rounded-full`}>{spreadLabel}</span>
                                <span className="inline-flex items-center text-sm font-medium px-3 py-1 rounded-full bg-gray-100 text-gray-700">Volatility: {displayedVolatility?.toFixed(2)}%</span>
                                <span className={`inline-flex items-center text-sm font-medium px-3 py-1 rounded-full ${spreadVal > 0 ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>Spread: {spreadVal.toFixed(2)}%</span>
                            </div>

                            <p className="text-sm text-gray-500 mt-3">Start Spread: {possiblePositionDTO?.positionModel.startSpread.toFixed(2)}%</p>
                                <p className="text-sm text-gray-500">Summary Tariff: {possiblePositionDTO?.positionModel.summaryTarrif.toFixed(4)}%</p>
                                <p className="text-base font-bold mt-2">Estimated Profit: {possiblePositionDTO?.positionModel.possibleProfit.toFixed(2)}%</p>
                                {totalSlippage != null && (
                                    <div className="mt-2 flex flex-wrap gap-2">
                                        <span className={`inline-flex items-center text-sm font-medium px-3 py-1 rounded-full ${totalSlippage > Math.abs(spreadVal) ? 'bg-red-100 text-red-700' : 'bg-yellow-100 text-yellow-700'}`}>
                                            Est. Slippage: {totalSlippage.toFixed(3)}%
                                        </span>
                                        {positionSize > 0 && (
                                            <span className="inline-flex items-center text-sm font-medium px-3 py-1 rounded-full bg-gray-100 text-gray-600">
                                                for ${positionSize.toLocaleString()} position
                                            </span>
                                        )}
                                    </div>
                                )}
                        </div>

                        <div className="md:col-span-1">
                            <p className="text-xs text-gray-500 mb-2">
                                Tip: click an exchange name to open its trading page directly.
                            </p>
                            <div>
                                <h3 className="text-sm font-medium text-gray-600">Short Exchange</h3>
                                <p className="text-gray-800">
                                    {possiblePositionDTO?.shortExchangeUrl ? (
                                        <a
                                            href={possiblePositionDTO.shortExchangeUrl}
                                            target="_blank"
                                            rel="noopener noreferrer"
                                            className="text-indigo-600 hover:underline"
                                        >
                                            {possiblePositionDTO.positionModel.exchangeShort.exchange}
                                        </a>
                                    ) : (
                                        possiblePositionDTO?.positionModel.exchangeShort.exchange
                                    )}
                                    {': '}{displayedShortRate}
                                </p>
                                {possiblePositionDTO?.positionModel.exchangeShort.fundingRateValue != null && possiblePositionDTO.positionModel.exchangeShort.fundingRateValue !== 0 && (
                                    <p className="text-sm text-gray-500">
                                        Funding: {(possiblePositionDTO.positionModel.exchangeShort.fundingRateValue * 100).toFixed(2)}% ({getFundingDirectionText(possiblePositionDTO.positionModel.exchangeShort.fundingRateValue, false)})
                                    </p>
                                )}
                            </div>
                            <hr className="my-3" />
                            <div>
                                <h3 className="text-sm font-medium text-gray-600">Long Exchange</h3>
                                <p className="text-gray-800">
                                    {possiblePositionDTO?.longExchangeUrl ? (
                                        <a
                                            href={possiblePositionDTO.longExchangeUrl}
                                            target="_blank"
                                            rel="noopener noreferrer"
                                            className="text-indigo-600 hover:underline"
                                        >
                                            {possiblePositionDTO.positionModel.exchangeLong.exchange}
                                        </a>
                                    ) : (
                                        possiblePositionDTO?.positionModel.exchangeLong.exchange
                                    )}
                                    {': '}{displayedLongRate}
                                </p>
                                {!isSpotSpread && possiblePositionDTO?.positionModel.exchangeLong.fundingRateValue != null && possiblePositionDTO.positionModel.exchangeLong.fundingRateValue !== 0 && (
                                    <p className="text-sm text-gray-500">
                                        Funding: {(possiblePositionDTO.positionModel.exchangeLong.fundingRateValue * 100).toFixed(2)}% ({getFundingDirectionText(possiblePositionDTO.positionModel.exchangeLong.fundingRateValue, true)})
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
                            <div className="">
                                <OrderBlock title={pos?.exchangeRateA.exchange} asks={asksA} bids={bidsA} />
                            </div>
                            <div className="">
                                <OrderBlock title={pos?.exchangeRateB.exchange} asks={asksB} bids={bidsB} />
                            </div>
                        </div>
                    ) : null;
                })()}
            </div>

        </div>
    );
}

export default SpreadPage;