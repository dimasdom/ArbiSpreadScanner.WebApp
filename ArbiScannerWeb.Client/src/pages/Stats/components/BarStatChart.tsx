import React, { useMemo } from 'react';
import Chart from 'react-apexcharts';
import type { ApexOptions } from 'apexcharts';
import { useTheme } from '../../../contexts/ThemeContext';

interface BarStatChartProps {
    categories: string[];
    values: number[];
    valueSuffix?: string;
    emptyMessage: string;
}

const BarStatChart: React.FC<BarStatChartProps> = ({ categories, values, valueSuffix = '', emptyMessage }) => {
    const { theme } = useTheme();

    const series = useMemo(() => [{ name: 'value', data: values }], [values]);

    const options: ApexOptions = useMemo(() => ({
        chart: {
            type: 'bar',
            height: 320,
            background: 'transparent',
            toolbar: { show: false },
        },
        theme: {
            mode: theme,
        },
        plotOptions: {
            bar: {
                horizontal: true,
                borderRadius: 4,
                distributed: true,
            },
        },
        legend: {
            show: false,
        },
        dataLabels: {
            enabled: true,
            formatter: (val: number) => `${val.toLocaleString(undefined, { maximumFractionDigits: 2 })}${valueSuffix}`,
        },
        xaxis: {
            categories,
            labels: {
                formatter: (val: string) => `${Number(val).toLocaleString(undefined, { maximumFractionDigits: 2 })}${valueSuffix}`,
            },
        },
        tooltip: {
            y: {
                formatter: (val: number) => `${val.toLocaleString(undefined, { maximumFractionDigits: 2 })}${valueSuffix}`,
            },
        },
    }), [theme, categories, valueSuffix]);

    if (categories.length === 0) {
        return (
            <div className="flex items-center justify-center h-80 rounded-2xl bg-white dark:bg-gray-800 text-gray-500 dark:text-gray-400 text-sm transition-colors duration-200">
                {emptyMessage}
            </div>
        );
    }

    return (
        <div className="overflow-x-auto w-full rounded-2xl bg-white dark:bg-gray-800 p-2 transition-colors duration-200">
            <Chart options={options} series={series} type="bar" height={320} width="100%" />
        </div>
    );
};

export default BarStatChart;
