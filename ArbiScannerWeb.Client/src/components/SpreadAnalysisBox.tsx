import { useTranslation } from 'react-i18next';
import SpreadCard from './SpreadCard';
import type { TradeOpportunityDetailsDTO } from '../types/tradeOpportunityModel';

interface SpreadAnalysisBoxProps {
    spread: TradeOpportunityDetailsDTO;
}

export default function SpreadAnalysisBox({ spread }: Readonly<SpreadAnalysisBoxProps>) {
    const { t } = useTranslation('chat');
    const analysis = spread?.analysis;

    return (
        <div className="space-y-2">
            <SpreadCard dto={spread} />
            {analysis && (
                <>
                    <span
                        className={`inline-flex items-center text-sm font-medium px-3 py-1 rounded-full ${
                            analysis.recommended
                                ? 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400'
                                : 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'
                        }`}
                    >
                        {analysis.recommended ? t('widget.recommended') : t('widget.notRecommended')}
                    </span>
                    <ul className="list-disc list-inside text-sm text-gray-700 dark:text-gray-300 space-y-0.5">
                        {(analysis.reasons ?? []).map((reason) => (
                            <li key={reason}>{reason}</li>
                        ))}
                    </ul>
                    {analysis.trendWarning && (
                        <p className="text-sm rounded-lg bg-amber-50 dark:bg-amber-950 text-amber-800 dark:text-amber-300 p-2">
                            {analysis.trendWarning}
                        </p>
                    )}
                </>
            )}
        </div>
    );
}
