import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { PossiblePositionTickerModel } from '../../types/tickerType';

vi.mock('react-apexcharts', () => ({
    default: (props: { options: unknown; series: unknown; type: string }) => (
        <div data-testid="apex-chart" data-type={props.type}>{JSON.stringify(props.series)}</div>
    ),
}));

const tickers: PossiblePositionTickerModel[] = [
    { id: 1, guid: 'g1', symbol: 'BTC/USDT', spread: 1, echangeA: 'binance', exchangeB: 'okx', exchangeLong: 'okx', exchangeShort: 'binance', rateA: 100, rateB: 101, dateTime: '2026-01-01T00:00:00Z' },
];

describe('ChartComponent (CandleChart)', () => {
    it('renders a candlestick chart with the ticker data converted to OHLC series', async () => {
        const { default: CandleChart } = await import('./ChartComponent');
        render(<CandleChart tittle="BTC/USDT" ticker={tickers} />);

        const chart = screen.getByTestId('apex-chart');
        expect(chart).toHaveAttribute('data-type', 'candlestick');
        expect(chart.textContent).toContain('2026-01-01');
    });

    it('renders with an empty ticker list', async () => {
        const { default: CandleChart } = await import('./ChartComponent');
        render(<CandleChart tittle={null} ticker={[]} />);

        expect(screen.getByTestId('apex-chart').textContent).toBe('[{"data":[]}]');
    });
});
