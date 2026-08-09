import { useTranslation } from 'react-i18next';
import Link from './LocalizedLink';
import SpreadPill from './SpreadPill';
import type { TradeOpportunityDetailsDTO } from '../types/tradeOpportunityModel';

interface SpreadCardProps {
    dto: TradeOpportunityDetailsDTO;
}

export default function SpreadCard({ dto }: Readonly<SpreadCardProps>) {
    const { t } = useTranslation('chat');
    const positionModel = dto?.positionModel;

    // Tool data crosses an MCP/LLM boundary - a malformed or unexpectedly-shaped item
    // shouldn't take down the whole chat widget, just quietly not render that one card.
    if (!positionModel) return null;

    return (
        <div className="rounded-lg border border-gray-200 dark:border-gray-700 p-3 flex items-center justify-between gap-3">
            <div className="min-w-0">
                <div className="font-medium text-gray-800 dark:text-gray-100 truncate">{positionModel.symbol}</div>
                <div className="text-xs text-gray-500 dark:text-gray-400 truncate">
                    {positionModel.exchangeLong.exchange} / {positionModel.exchangeShort.exchange}
                </div>
            </div>
            <div className="flex items-center gap-2 shrink-0">
                <SpreadPill spreadPercent={positionModel.spread} />
                <Link
                    to={`/spread?id=${positionModel.guid}`}
                    className="text-xs font-medium text-indigo-600 dark:text-indigo-400 hover:underline"
                >
                    {t('widget.viewSpread')}
                </Link>
            </div>
        </div>
    );
}
