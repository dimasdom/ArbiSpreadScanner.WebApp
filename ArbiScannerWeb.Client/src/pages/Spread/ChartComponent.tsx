// CandleChart.tsx
import React, { useMemo } from 'react';
import Chart from 'react-apexcharts';
import type { ApexOptions } from 'apexcharts';
import type { PossiblePositionTickerModel } from '../../types/tickerType';
import { convertToOHLC } from '../../utils/chartUtils';

interface ChartProps {
    tittle: string | undefined | null;
    ticker: PossiblePositionTickerModel[];
}
const CandleChart: React.FC<ChartProps> = ({ ticker, tittle }) => {
    const series = useMemo(() => [{ data: convertToOHLC(ticker) }], [ticker]);
    

    const options: ApexOptions = {
        chart: {
            type: 'candlestick',
            height: 600,
            animations: {
                enabled: true, // can be enabled for live updates
            },
        },
        title: {
            text: tittle ?? undefined,
            align: 'left',
        },
        xaxis: {
            type: 'datetime',
        },
        yaxis: {
            tooltip: {
                enabled: true,
            },
        },
        tooltip: {
            enabled: true,
            x: {
                format: 'dd MMM HH:mm',
            },
        },
    };

    return (
        <Chart options={options} series={series} type="candlestick" height={600} width={800} />
    );
};

export default CandleChart;
