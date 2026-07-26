import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { PossiblePositionTickerModel } from '../../types/tickerType';

const useThemeMock = vi.fn();

vi.mock('react-apexcharts', () => ({
    default: (props: { options: { title?: { text?: string } }; type: string }) => (
        <div data-testid="apex-chart" data-type={props.type}>{props.options.title?.text}</div>
    ),
}));
vi.mock('../../contexts/ThemeContext', () => ({ useTheme: () => useThemeMock() }));

const tickers: PossiblePositionTickerModel[] = [
    { id: 1, guid: 'g1', symbol: 'BTC/USDT', spread: 1, echangeA: 'binance', exchangeB: 'okx', exchangeLong: 'okx', exchangeShort: 'binance', rateA: 100, rateB: 101, dateTime: '2026-01-01T00:00:00Z' },
];

describe('RealtimeLineChart', () => {
    it('renders a line chart with the given title', async () => {
        useThemeMock.mockReturnValue({ theme: 'light' });
        const { default: RealtimeLineChart } = await import('./RealTimeLineChartComponent');

        render(<RealtimeLineChart title="BTC/USDT Spread" ticker={tickers} />);

        const chart = screen.getByTestId('apex-chart');
        expect(chart).toHaveAttribute('data-type', 'line');
        expect(chart.textContent).toBe('BTC/USDT Spread');
    });

    it('falls back to the default title when none is given', async () => {
        useThemeMock.mockReturnValue({ theme: 'dark' });
        const { default: RealtimeLineChart } = await import('./RealTimeLineChartComponent');

        render(<RealtimeLineChart title={null} ticker={tickers} />);

        expect(screen.getByTestId('apex-chart').textContent).toBe('spreadPage.chart.defaultTitle');
    });
});
