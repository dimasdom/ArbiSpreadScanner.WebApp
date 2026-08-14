import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { ApexOptions } from 'apexcharts';

const useThemeMock = vi.fn();
const { capturedOptions } = vi.hoisted(() => ({ capturedOptions: { current: null as ApexOptions | null } }));

vi.mock('react-apexcharts', () => ({
    default: (props: { options: ApexOptions; series: unknown; type: string }) => {
        capturedOptions.current = props.options;
        return <div data-testid="apex-chart" data-type={props.type}>{JSON.stringify({ series: props.series, labels: props.options.labels })}</div>;
    },
}));
vi.mock('../../../contexts/ThemeContext', () => ({ useTheme: () => useThemeMock() }));

describe('DonutStatChart', () => {
    it('renders the empty message when the series is empty', async () => {
        useThemeMock.mockReturnValue({ theme: 'light' });
        const { default: DonutStatChart } = await import('./DonutStatChart');

        render(<DonutStatChart labels={[]} series={[]} emptyMessage="No data yet" />);

        expect(screen.getByText('No data yet')).toBeInTheDocument();
        expect(screen.queryByTestId('apex-chart')).not.toBeInTheDocument();
    });

    it('renders the empty message when every value in the series is zero', async () => {
        useThemeMock.mockReturnValue({ theme: 'light' });
        const { default: DonutStatChart } = await import('./DonutStatChart');

        render(<DonutStatChart labels={['Futures', 'Spot']} series={[0, 0]} emptyMessage="No data yet" />);

        expect(screen.getByText('No data yet')).toBeInTheDocument();
    });

    it('renders a donut chart with the given labels and series', async () => {
        useThemeMock.mockReturnValue({ theme: 'dark' });
        const { default: DonutStatChart } = await import('./DonutStatChart');

        render(<DonutStatChart labels={['Futures', 'Spot']} series={[5, 3]} emptyMessage="No data yet" />);

        const chart = screen.getByTestId('apex-chart');
        expect(chart).toHaveAttribute('data-type', 'donut');
        expect(JSON.parse(chart.textContent ?? '{}')).toEqual({ series: [5, 3], labels: ['Futures', 'Spot'] });
    });

    it('formats data labels as a percentage with one decimal place', async () => {
        useThemeMock.mockReturnValue({ theme: 'light' });
        const { default: DonutStatChart } = await import('./DonutStatChart');

        render(<DonutStatChart labels={['Futures', 'Spot']} series={[5, 3]} emptyMessage="No data yet" />);

        const formatter = capturedOptions.current?.dataLabels?.formatter as (val: number) => string;
        expect(formatter(62.5)).toBe('62.5%');
    });
});
