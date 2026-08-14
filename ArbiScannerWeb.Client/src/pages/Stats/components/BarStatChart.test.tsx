import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

const useThemeMock = vi.fn();

vi.mock('react-apexcharts', () => ({
    default: (props: { options: { xaxis?: { categories?: string[] } }; series: unknown; type: string }) => (
        <div data-testid="apex-chart" data-type={props.type}>{JSON.stringify({ series: props.series, categories: props.options.xaxis?.categories })}</div>
    ),
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
});
