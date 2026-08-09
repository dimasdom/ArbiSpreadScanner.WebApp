import { useTranslation } from 'react-i18next';
import Link from './LocalizedLink';
import SpreadCard from './SpreadCard';
import type { TradeOpportunityDetailsDTO } from '../types/tradeOpportunityModel';

interface SpreadsPreviewBoxProps {
    spreads: TradeOpportunityDetailsDTO[];
    totalCount: number;
}

export default function SpreadsPreviewBox({ spreads, totalCount }: Readonly<SpreadsPreviewBoxProps>) {
    const { t } = useTranslation('chat');
    const remaining = totalCount - spreads.length;

    return (
        <div className="space-y-2">
            {spreads.map((dto, index) => (
                <SpreadCard key={dto?.positionModel?.guid ?? index} dto={dto} />
            ))}
            {remaining > 0 && (
                <Link
                    to="/spreads"
                    className="block text-sm font-medium text-indigo-600 dark:text-indigo-400 hover:underline"
                >
                    {t('widget.viewOnSpreadsPage', { count: remaining })}
                </Link>
            )}
        </div>
    );
}
