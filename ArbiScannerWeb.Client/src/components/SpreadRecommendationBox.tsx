import { useTranslation } from 'react-i18next';
import SpreadCard from './SpreadCard';
import { SpreadType } from '../types/SpreadType';
import type { RecommendedSpreadDTO } from '../types/tradeOpportunityModel';

interface SpreadRecommendationBoxProps {
    spreads: RecommendedSpreadDTO[];
}

const CATEGORY_ORDER = [SpreadType.Futures, SpreadType.Funding, SpreadType.Spot];

const CATEGORY_KEYS: Record<SpreadType, string> = {
    [SpreadType.Futures]: 'spreadType.Futures',
    [SpreadType.Funding]: 'spreadType.Funding',
    [SpreadType.Spot]: 'spreadType.Spot',
};

export default function SpreadRecommendationBox({ spreads }: Readonly<SpreadRecommendationBoxProps>) {
    const { t } = useTranslation(['chat', 'spreads']);

    if (spreads.length === 0) {
        return <p className="text-sm text-gray-500 dark:text-gray-400">{t('widget.noRecommendations')}</p>;
    }

    return (
        <div className="space-y-3">
            {CATEGORY_ORDER.map((category) => {
                const items = spreads.filter((s) => s.category === category);
                if (items.length === 0) return null;

                return (
                    <div key={category}>
                        <div className="text-xs font-semibold uppercase text-gray-500 dark:text-gray-400 mb-1">
                            {t(CATEGORY_KEYS[category], { ns: 'spreads' })}
                        </div>
                        <div className="space-y-2">
                            {items.map((item, index) => (
                                <SpreadCard key={item.details?.positionModel?.guid ?? index} dto={item.details} />
                            ))}
                        </div>
                    </div>
                );
            })}
        </div>
    );
}
