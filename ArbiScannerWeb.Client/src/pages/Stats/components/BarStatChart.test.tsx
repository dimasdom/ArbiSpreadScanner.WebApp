import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { ApexOptions } from 'apexcharts';

const useThemeMock = vi.fn();
const { capturedOptions } = vi.hoisted(() => ({ capturedOptions: { current: null as ApexOptions | null } }));

vi.mock('react-apexcharts', () => ({
    default: (props: { options: ApexOptions; series: unknown; type: string }) => {
        capturedOptions.current = props.options;
        return <div data-testid="apex-chart" data-type={props.type}>{JSON.stringify({ series: props.series, categories: props.options.xaxis?.categories })}</div>;
    },
}));
vi.mock('../../../contexts/ThemeContext', () => ({ useTheme: () => useThemeMock() }));

describe('BarStatChart', () => {
    it('renders the empty message when there are no categories', async () => {
        useThemeMock.mockReturnValue({ theme: 'light' });
        const { default: BarStatChart } = await import('./BarStatChart');

        render(<BarStatChart categories={[]} values={[]} emptyMessage="No data yet" />);

        expect(screen.getByText('No data yet')).toBeInTheDocument();
        expect(screen.queryByTestId('apex-chart')).not.toBeInTheDocument();
    });

    it('renders a horizontal bar chart with the given categories and values', async () => {
        useThemeMock.mockReturnValue({ theme: 'dark' });
        const { default: BarStatChart } = await import('./BarStatChart');

        render(<BarStatChart categories={['BTC/USDT', 'ETH/USDT']} values={[1.5, 0.8]} valueSuffix="%" emptyMessage="No data yet" />);

        const chart = screen.getByTestId('apex-chart');
        expect(chart).toHaveAttribute('data-type', 'bar');
        expect(JSON.parse(chart.textContent ?? '{}')).toEqual({
            series: [{ name: 'value', data: [1.5, 0.8] }],
            categories: ['BTC/USDT', 'ETH/USDT'],
        });
    });

    it('appends the value suffix when formatting data labels, x-axis labels, and tooltips', async () => {
        useThemeMock.mockReturnValue({ theme: 'dark' });
        const { default: BarStatChart } = await import('./BarStatChart');

        render(<BarStatChart categories={['BTC/USDT']} values={[1234.567]} valueSuffix="%" emptyMessage="No data yet" />);

        const expected = `${(1234.567).toLocaleString(undefined, { maximumFractionDigits: 2 })}%`;
        const { dataLabels, xaxis, tooltip } = capturedOptions.current!;
        const tooltipY = tooltip!.y as { formatter: (val: number) => string };
        expect((dataLabels!.formatter as (val: number) => string)(1234.567)).toBe(expected);
        expect((xaxis!.labels!.formatter as (val: string) => string)('1234.567')).toBe(expected);
        expect(tooltipY.formatter(1234.567)).toBe(expected);
    });
});
