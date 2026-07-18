import React, { useMemo } from 'react';
import Chart from 'react-apexcharts';
import type { ApexOptions } from 'apexcharts';
import { useTranslation } from 'react-i18next';
import type { PossiblePositionTickerModel } from '../../types/tickerType';
import { convertToSeries } from '../../utils/chartUtils';
import { useChartWidth } from '../../hooks/useChartWidth';
import { useTheme } from '../../contexts/ThemeContext';

interface RealtimeChartProps {
    title: string | undefined | null;
    ticker: PossiblePositionTickerModel[];
}

export const RealtimeLineChart: React.FC<RealtimeChartProps> = ({ title, ticker }) => {
    const chartWidth = useChartWidth(0.8);
    const { theme } = useTheme();
    const { t } = useTranslation('spreads');
    const series = useMemo(
        () => [{ data: convertToSeries(ticker) }],
        [ticker],
    );

    const options: ApexOptions = {
        chart: {
            id: 'realtime',
            type: 'line',
            height: 350,
            background: 'transparent',
            animations: {
                enabled: true,
                easing: 'linear',
                dynamicAnimation: {
                    speed: 1000,
                },
            },
            toolbar: {
                show: true,
                tools: {
                    zoom: true,
                    zoomin: true,
                    zoomout: true,
                    pan: true,
                    reset: true,
                    selection: true,
                },
                autoSelected: 'zoom',
            },
            zoom: {
                enabled: true,
                type: 'xy',
                autoScaleYaxis: false,
            },
            pan: {
                enabled: true,
                type: 'xy',
                freeMove: true,
            },
        },
        theme: {
            mode: theme,
        },
        title: {
            text: title || t('spreadPage.chart.defaultTitle'),
            align: 'left',
        },
        dataLabels: {
            enabled: false,
        },
        stroke: {
            curve: 'smooth',
        },
        markers: {
            size: 0,
        },
        xaxis: {
            type: 'datetime',
            tickAmount: 10,
            labels: {
                datetimeUTC: false,
            },
        },
        yaxis: {
            tooltip: {
                enabled: true,
            },
            decimalsInFloat: 2,
            forceNiceScale: true,
        },
        tooltip: {
            x: {
                format: 'dd MMM HH:mm:ss',
            },
        },
        legend: {
            show: false,
        },
    };

    return (
        <div className='overflow-x-auto w-full mx-auto shadow-lg rounded-2xl bg-white dark:bg-gray-800 transition-colors duration-200'>
            <div className='p-2 flex justify-center overflow-x-hidden overflow-y-hidden'>
                <Chart options={options} series={series} type="line" height={350} width={chartWidth} />
            </div>
        </div>
    );
};

export default RealtimeLineChart;
