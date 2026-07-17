import { useMemo } from 'react';
import { useNavigate } from 'react-router';
import type { GridRowParams } from '@mui/x-data-grid';
import { useGetSpreadsQuery } from '../../../store/services/spread';
import { useIsMobile } from '../../../hooks/useIsMobile';

const typeLabels: Record<number, string> = {
    2: 'Spot',
    0: 'Futures',
    1: 'Funding',
};

export function useSpreadsPage() {
    const { data, isLoading, isError } = useGetSpreadsQuery();
    const navigate = useNavigate();
    const isMobile = useIsMobile();

    const possiblePositions = useMemo(() => data?.value ?? [], [data]);

    const rows = useMemo(
        () =>
            possiblePositions.map((item) => ({
                id: item.guid,
                guid: item.guid,
                spread: Math.abs(item.spread).toFixed(4),
                type:
                    item.type != null && item.type !== undefined
                        ? typeLabels[Number(item.type)]
                        : item.type,
                symbol: item.symbol,
                exchangeLong: item.exchangeLong.exchange,
                exchangeShort: item.exchangeShort.exchange,
                exchangeRateLong: item.exchangeLong.exchangeRate.toFixed(6),
                exchangeRateShort: item.exchangeShort.exchangeRate.toFixed(6),
                fundingRateLong: item.exchangeLong.fundingRateValue ? (item.exchangeLong.fundingRateValue * 100).toFixed(4) + '%' : 'N/A',
                fundingRateShort: item.exchangeShort.fundingRateValue
                    ? (item.exchangeShort.fundingRateValue * 100).toFixed(4) + '%'
                    : 'N/A',
            })),
        [possiblePositions],
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
