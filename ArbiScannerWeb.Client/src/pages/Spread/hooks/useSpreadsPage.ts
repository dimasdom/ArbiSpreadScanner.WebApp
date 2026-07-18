import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { GridRowParams } from '@mui/x-data-grid';
import { useGetSpreadsQuery } from '../../../store/services/spread';
import { useIsMobile } from '../../../hooks/useIsMobile';
import { useLocalizedNavigate } from '../../../i18n/routing';

const typeKeys: Record<number, string> = {
    2: 'spreadType.Spot',
    0: 'spreadType.Futures',
    1: 'spreadType.Funding',
};

export function useSpreadsPage() {
    const { data, isLoading, isError } = useGetSpreadsQuery();
    const navigate = useLocalizedNavigate();
    const isMobile = useIsMobile();
    const { t } = useTranslation('spreads');

    const possiblePositions = useMemo(() => data?.value ?? [], [data]);

    const rows = useMemo(
        () =>
            possiblePositions.map((item) => ({
                id: item.guid,
                guid: item.guid,
                spread: Math.abs(item.spread).toFixed(4),
                type:
                    item.type != null && item.type !== undefined
                        ? t(typeKeys[Number(item.type)])
                        : item.type,
                symbol: item.symbol,
                exchangeLong: item.exchangeLong.exchange,
                exchangeShort: item.exchangeShort.exchange,
                exchangeRateLong: item.exchangeLong.exchangeRate.toFixed(6),
                exchangeRateShort: item.exchangeShort.exchangeRate.toFixed(6),
                fundingRateLong: item.exchangeLong.fundingRateValue ? (item.exchangeLong.fundingRateValue * 100).toFixed(4) + '%' : t('spreadsPage.notAvailable'),
                fundingRateShort: item.exchangeShort.fundingRateValue
                    ? (item.exchangeShort.fundingRateValue * 100).toFixed(4) + '%'
                    : t('spreadsPage.notAvailable'),
            })),
        [possiblePositions, t],
    );

    const handleRowDoubleClick = (params: GridRowParams) => {
        navigate(`/spread?id=${params.row.guid}`);
    };

    const handleRowClick = (params: GridRowParams) => {
        if (isMobile) {
            navigate(`/spread?id=${params.row.guid}`);
        }
    };

    return { rows, isLoading, isError, isMobile, handleRowDoubleClick, handleRowClick };
}
