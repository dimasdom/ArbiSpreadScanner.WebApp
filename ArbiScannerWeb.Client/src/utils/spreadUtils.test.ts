import { describe, it, expect } from 'vitest';
import { calcVolatility, calcSlippage, getFundingDirectionText } from './spreadUtils';
import type { OrderBlock } from '../types/tradeOpportunityModel';

describe('calcVolatility', () => {
    it('returns 0 for empty array', () => {
        expect(calcVolatility([])).toBe(0);
    });

    it('returns 0 for single price', () => {
        expect(calcVolatility([100])).toBe(0);
    });

    it('returns 0 for two identical prices', () => {
        expect(calcVolatility([100, 100])).toBe(0);
    });

    it('returns positive value for varying prices', () => {
        expect(calcVolatility([100, 110, 90, 105])).toBeGreaterThan(0);
    });

    it('returns higher value for more volatile prices', () => {
        const lowVol = calcVolatility([100, 101, 100, 101]);
        const highVol = calcVolatility([100, 150, 80, 130]);
        expect(highVol).toBeGreaterThan(lowVol);
    });
});

describe('calcSlippage', () => {
    it('returns null for null levels', () => {
        expect(calcSlippage(null, 'ask', 1000)).toBeNull();
    });

    it('returns null for undefined levels', () => {
        expect(calcSlippage(undefined, 'ask', 1000)).toBeNull();
    });

    it('returns null for empty levels array', () => {
        expect(calcSlippage([], 'ask', 1000)).toBeNull();
    });

    it('returns null for zero sizeUsd', () => {
        const levels: OrderBlock[] = [{ price: 100, amount: 10 }];
        expect(calcSlippage(levels, 'ask', 0)).toBeNull();
    });

    it('returns null for negative sizeUsd', () => {
        const levels: OrderBlock[] = [{ price: 100, amount: 10 }];
        expect(calcSlippage(levels, 'ask', -500)).toBeNull();
    });

    it('returns 0 slippage when single level fully covers ask order', () => {
        const levels: OrderBlock[] = [{ price: 100, amount: 100 }];
        const result = calcSlippage(levels, 'ask', 500);
        expect(result).toBeCloseTo(0, 5);
    });

    it('returns positive slippage for ask side with multiple levels', () => {
        const levels: OrderBlock[] = [
            { price: 100, amount: 5 },
            { price: 101, amount: 10 },
        ];
        const result = calcSlippage(levels, 'ask', 1000);
        expect(result).toBeGreaterThan(0);
    });

    it('returns positive slippage for bid side (realised price below best bid)', () => {
        const levels: OrderBlock[] = [
            { price: 101, amount: 5 },
            { price: 100, amount: 10 },
        ];
        const result = calcSlippage(levels, 'bid', 1000);
        expect(result).toBeGreaterThan(0);
    });

    it('sorts ask levels ascending by price', () => {
        const unsorted: OrderBlock[] = [
            { price: 102, amount: 10 },
            { price: 100, amount: 5 },
            { price: 101, amount: 5 },
        ];
        const result = calcSlippage(unsorted, 'ask', 500);
        expect(result).toBeCloseTo(0, 5);
    });

    it('sorts bid levels descending by price', () => {
        const unsorted: OrderBlock[] = [
            { price: 98, amount: 10 },
            { price: 100, amount: 5 },
            { price: 99, amount: 5 },
        ];
        const result = calcSlippage(unsorted, 'bid', 500);
        expect(result).toBeCloseTo(0, 5);
    });
});

describe('getFundingDirectionText', () => {
    it('returns "You pay" when rate is positive and position is long', () => {
        expect(getFundingDirectionText(0.01, true)).toBe('You pay');
    });

    it('returns "You receive" when rate is positive and position is short', () => {
        expect(getFundingDirectionText(0.01, false)).toBe('You receive');
    });

    it('returns "You receive" when rate is negative and position is long', () => {
        expect(getFundingDirectionText(-0.01, true)).toBe('You receive');
    });

    it('returns "You pay" when rate is negative and position is short', () => {
        expect(getFundingDirectionText(-0.01, false)).toBe('You pay');
    });

    it('returns "You receive" when rate is zero and position is long', () => {
        expect(getFundingDirectionText(0, true)).toBe('You receive');
    });

    it('returns "You pay" when rate is zero and position is short', () => {
        expect(getFundingDirectionText(0, false)).toBe('You pay');
    });
});
