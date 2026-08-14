import React, { useMemo } from 'react';
import Chart from 'react-apexcharts';
import type { ApexOptions } from 'apexcharts';
import { useTheme } from '../../../contexts/ThemeContext';

interface DonutStatChartProps {
    labels: string[];
    series: number[];
    emptyMessage: string;
}

const DonutStatChart: React.FC<DonutStatChartProps> = ({ labels, series, emptyMessage }) => {
    const { theme } = useTheme();

    const options: ApexOptions = useMemo(() => ({
        chart: {
            type: 'donut',
            height: 320,
            background: 'transparent',
        },
        theme: {
            mode: theme,
        },
        labels,
        legend: {
            position: 'bottom',
        },
        dataLabels: {
            enabled: true,
            formatter: (val: number) => `${val.toFixed(1)}%`,
        },
    }), [theme, labels]);

    const hasData = series.length > 0 && series.some((value) => value > 0);

    if (!hasData) {
        return (
            <div className="flex items-center justify-center h-80 rounded-2xl bg-white dark:bg-gray-800 text-gray-500 dark:text-gray-400 text-sm transition-colors duration-200">
                {emptyMessage}
            </div>
        );
    }

    return (
        <div className="overflow-x-auto w-full rounded-2xl bg-white dark:bg-gray-800 p-2 transition-colors duration-200">
            <Chart options={options} series={series} type="donut" height={320} width="100%" />
        </div>
    );
};

export default DonutStatChart;
