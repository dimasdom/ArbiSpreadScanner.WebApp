import { describe, it, expect } from 'vitest';
import { convertToOHLC, convertToSeries } from './chartUtils';
import type { PossiblePositionTickerModel } from '../types/tickerType';

function makeTicker(spread: number, dateTime: string): PossiblePositionTickerModel {
    return {
        id: 1,
        guid: 'test-guid',
        symbol: 'BTC/USDT',
        spread,
        echangeA: 'Binance',
        exchangeB: 'Bybit',
        exchangeLong: 'Binance',
        exchangeShort: 'Bybit',
        rateA: 100,
        rateB: 101,
        dateTime,
    };
}

describe('convertToOHLC', () => {
    it('returns empty array for empty input', () => {
        expect(convertToOHLC([])).toEqual([]);
    });

    it('returns single candle for one ticker', () => {
        const tickers = [makeTicker(1.5, '2025-01-01T10:00:00Z')];
        const result = convertToOHLC(tickers);
        expect(result).toHaveLength(1);
        const [candle] = result;
        expect(candle.y).toEqual([1.5, 1.5, 1.5, 1.5]);
    });

    it('groups tickers in the same minute into one candle', () => {
        const tickers = [
            makeTicker(1.0, '2025-01-01T10:00:10Z'),
            makeTicker(1.5, '2025-01-01T10:00:30Z'),
            makeTicker(0.8, '2025-01-01T10:00:50Z'),
        ];
        const result = convertToOHLC(tickers);
        expect(result).toHaveLength(1);
        const [{ y }] = result;
        const [open, high, low, close] = y;
        expect(open).toBe(1.0);
        expect(high).toBe(1.5);
        expect(low).toBe(0.8);
        expect(close).toBe(0.8);
    });

    it('creates separate candles for different minutes', () => {
        const tickers = [
            makeTicker(1.0, '2025-01-01T10:00:00Z'),
            makeTicker(2.0, '2025-01-01T10:01:00Z'),
        ];
        const result = convertToOHLC(tickers);
        expect(result).toHaveLength(2);
    });

    it('sorts candles chronologically', () => {
        const tickers = [
            makeTicker(2.0, '2025-01-01T10:02:00Z'),
            makeTicker(1.0, '2025-01-01T10:00:00Z'),
            makeTicker(3.0, '2025-01-01T10:01:00Z'),
        ];
        const result = convertToOHLC(tickers);
        expect(result).toHaveLength(3);
        expect(result[0].x.getTime()).toBeLessThan(result[1].x.getTime());
        expect(result[1].x.getTime()).toBeLessThan(result[2].x.getTime());
    });

    it('open = first tick, close = last tick within a minute', () => {
        const tickers = [
            makeTicker(3.0, '2025-01-01T10:00:00Z'),
            makeTicker(1.0, '2025-01-01T10:00:20Z'),
            makeTicker(2.0, '2025-01-01T10:00:40Z'),
        ];
        const result = convertToOHLC(tickers);
        const [open, , , close] = result[0].y;
        expect(open).toBe(3.0);
        expect(close).toBe(2.0);
    });
});

describe('convertToSeries', () => {
    it('returns empty array for empty input', () => {
        expect(convertToSeries([])).toEqual([]);
    });

    it('maps each ticker to {x: timestamp, y: spread}', () => {
        const dt = '2025-01-01T10:00:00Z';
        const tickers = [makeTicker(1.5, dt)];
        const result = convertToSeries(tickers);
        expect(result).toHaveLength(1);
        expect(result[0].x).toBe(new Date(dt).getTime());
        expect(result[0].y).toBe(1.5);
    });

    it('sorts points by timestamp ascending', () => {
        const tickers = [
            makeTicker(3.0, '2025-01-01T10:02:00Z'),
            makeTicker(1.0, '2025-01-01T10:00:00Z'),
            makeTicker(2.0, '2025-01-01T10:01:00Z'),
        ];
        const result = convertToSeries(tickers);
        expect(result[0].y).toBe(1.0);
        expect(result[1].y).toBe(2.0);
        expect(result[2].y).toBe(3.0);
    });

    it('filters out points with NaN timestamps', () => {
        const valid = makeTicker(1.5, '2025-01-01T10:00:00Z');
        const invalid = makeTicker(2.0, 'not-a-date');
        const result = convertToSeries([valid, invalid]);
        expect(result).toHaveLength(1);
        expect(result[0].y).toBe(1.5);
    });
});
