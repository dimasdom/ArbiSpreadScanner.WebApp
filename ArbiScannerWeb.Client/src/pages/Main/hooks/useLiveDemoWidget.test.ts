import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useLiveDemoWidget } from './useLiveDemoWidget';

describe('useLiveDemoWidget', () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('seeds an initial batch of tickers with the expected metadata', () => {
        const { result } = renderHook(() => useLiveDemoWidget());

        expect(result.current.tickers.length).toBeGreaterThan(1);
        expect(result.current.symbol).toBe('SOL/USDT');
        expect(result.current.exchangeA).toBe('Binance');
        expect(result.current.exchangeB).toBe('Bybit');
        expect(result.current.spreadVal).toBeGreaterThanOrEqual(0);
        expect(result.current.asksA?.length).toBeGreaterThan(0);
        expect(result.current.bidsB?.length).toBeGreaterThan(0);
    });

    it('appends a new ticker on each tick interval', () => {
        const { result } = renderHook(() => useLiveDemoWidget());
        const initialCount = result.current.tickers.length;

        act(() => { vi.advanceTimersByTime(3000); });

        expect(result.current.tickers.length).toBe(initialCount + 1);
    });

    it('caps the ticker history at the maximum length', () => {
        const { result } = renderHook(() => useLiveDemoWidget());

        act(() => { vi.advanceTimersByTime(3000 * 200); });

        expect(result.current.tickers.length).toBeLessThanOrEqual(100);
    });

    it('clears the interval on unmount', () => {
        const { result, unmount } = renderHook(() => useLiveDemoWidget());
        const countBeforeUnmount = result.current.tickers.length;

        unmount();
        act(() => { vi.advanceTimersByTime(3000 * 5); });

        expect(result.current.tickers.length).toBe(countBeforeUnmount);
    });
});
