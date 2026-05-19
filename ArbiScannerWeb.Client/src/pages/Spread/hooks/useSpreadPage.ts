import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { useSelector } from 'react-redux';
import type { IRootStore } from '../../../store/store';
import { useGetSpreadInfoQuery } from '../../../store/services/spread';
import { signalRService } from '../../../services/signalrService';
import { logger } from '../../../services/loggerService';
import type { TradeOpportunityDetailsDTO } from '../../../types/tradeOpportunityModel';
import type { MessageDTO, PossiblePositionTickerModel } from '../../../types/tickerType';
import { PositionAction } from '../../../types/PositionAction';
import { SpreadType } from '../../../types/SpreadType';
import { calcVolatility, calcSlippage } from '../../../utils/spreadUtils';

const SpreadTypeColors: Record<string, string> = {
    Futures: 'bg-indigo-100 text-indigo-700',
    Funding: 'bg-green-100 text-green-700',
    Spot: 'bg-amber-100 text-amber-700',
};

export function useSpreadPage() {
    const navigate = useNavigate();
    const [searchParams] = useSearchParams();
    const positionSize = useSelector((state: IRootStore) => state.account.account.userSettings?.positionSize ?? 0);

    const spreadId = searchParams.get('id');
    const { data, isLoading: isSpreadLoading, isError } = useGetSpreadInfoQuery(spreadId ?? '', {
        skip: !spreadId,
    });

    const [possiblePositionDTO, setPossiblePositionDTO] = useState<TradeOpportunityDetailsDTO>();
    const [tickers, setTickers] = useState<PossiblePositionTickerModel[]>([]);
    const [loading, setLoading] = useState(true);
    const [groupName, setGroupName] = useState('');
    const [shortRate, setShortRate] = useState<number | undefined>();
    const [longRate, setLongRate] = useState<number | undefined>();
    const [spreadVal, setSpreadVal] = useState<number>(0);
    const [isSpreadClosedDialogOpen, setIsSpreadClosedDialogOpen] = useState(false);

    useEffect(() => {
        if (tickers.length === 0) return;
        const last = tickers[tickers.length - 1];
        setShortRate(last.exchangeShort === last.echangeA ? last.rateA : last.rateB);
        setLongRate(last.exchangeLong === last.echangeA ? last.rateA : last.rateB);
        setSpreadVal(last.spread);
    }, [tickers]);

    useEffect(() => {
        const getData = async () => {
            if (!spreadId) {
                navigate('/spreads');
                return;
            }

            if (data?.isSuccess) {
                const spread = data.value;
                setPossiblePositionDTO(spread);
                setTickers([...spread.tickers].reverse());
                setSpreadVal(spread.positionModel.spread);
                setGroupName(
                    `${spread.positionModel.exchangeRateA.exchange}${spread.positionModel.exchangeRateB.exchange}${spread.positionModel.symbol}`,
                );
                setLoading(false);
                return;
            }

            if (isError) {
                navigate('/spreads');
            }
        };

        getData().catch((error: unknown) => {
            const details = error instanceof Error ? error.message : 'Unknown spread fetch error';
            logger.error('Failed to load spread data', 'SpreadPage', details);
            setLoading(false);
        });
    }, [spreadId, data, isError, navigate]);

    useEffect(() => {
        if (!groupName) return;
        let cancelled = false;

        const handleMessage = (message: MessageDTO) => {
            if (message.possiblePosition?.actionType === PositionAction.Close) {
                setIsSpreadClosedDialogOpen(true);
                return;
            }
            if (message.ticker) {
                setTickers((prev) => [...prev, message.ticker]);
            }
        };

        const start = async () => {
            await signalRService.connect('/hubs/TradeOpportunities');
            if (cancelled) return;
            await signalRService.joinGroup(groupName);
            if (cancelled) return;
            signalRService.onTickerUpdate(handleMessage);
        };

        start().catch((error: unknown) => {
            const details = error instanceof Error ? error.message : 'Unknown SignalR startup error';
            logger.error('SignalR startup failed', 'SpreadPage', details);
        });

        return () => {
            cancelled = true;
            signalRService.leaveGroup(groupName).catch(() => {});
            signalRService.offTickerUpdate();
            signalRService.disconnect().catch(() => {});
        };
    }, [groupName]);

    const handleSpreadClosedConfirm = () => {
        setIsSpreadClosedDialogOpen(false);
        navigate('/spreads');
    };

    const displayedVolatility = useMemo(() => {
        if (tickers.length < 2) return possiblePositionDTO?.positionModel.volatility;
        const volA = calcVolatility(tickers.map((t) => t.rateA));
        const volB = calcVolatility(tickers.map((t) => t.rateB));
        return Math.max(volA, volB);
    }, [tickers, possiblePositionDTO?.positionModel.volatility]);

    const pos = possiblePositionDTO?.positionModel;
    const lastTicker = tickers.length ? tickers[tickers.length - 1] : null;
    const asksA = lastTicker?.asksExchangeA ?? pos?.asksExchangeA;
    const bidsA = lastTicker?.bidsExchangeA ?? pos?.bidsExchangeA;
    const asksB = lastTicker?.asksExchangeB ?? pos?.asksExchangeB;
    const bidsB = lastTicker?.bidsExchangeB ?? pos?.bidsExchangeB;

    const shortExchangeIsA = pos?.exchangeShort.exchange === pos?.exchangeRateA.exchange;
    const slippageShort = calcSlippage(shortExchangeIsA ? bidsA : bidsB, 'bid', positionSize);
    const slippageLong = calcSlippage(shortExchangeIsA ? asksB : asksA, 'ask', positionSize);
    const totalSlippage =
        slippageShort != null && slippageLong != null
            ? Math.abs(slippageShort) + slippageLong
            : null;

    const spreadType = possiblePositionDTO?.positionModel.type;
    const spreadLabel = spreadType === undefined ? undefined : SpreadType[spreadType];
    const spreadClass = spreadLabel ? SpreadTypeColors[spreadLabel] : 'bg-gray-100 text-gray-700';

    return {
        possiblePositionDTO,
        tickers,
        isLoading: loading || isSpreadLoading,
        shortRate,
        longRate,
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
    };
}
