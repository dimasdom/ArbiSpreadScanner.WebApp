import type { OrderBlock } from '../types/tradeOpportunityModel';

export function calcVolatility(prices: number[]): number {
    if (prices.length < 2) return 0;
    const returns = prices.slice(1).map((p, i) => (p - prices[i]) / prices[i]);
    const mean = returns.reduce((a, b) => a + b, 0) / returns.length;
    const variance = returns.reduce((sum, r) => sum + (r - mean) ** 2, 0) / returns.length;
    return Math.sqrt(variance) * 100;
}

/**
 * Walks order book levels and computes slippage % for a given USD position size.
 * For asks (buying): levels sorted lowest price first.
 * For bids (selling): levels sorted highest price first.
 */
export function calcSlippage(
    levels: OrderBlock[] | null | undefined,
    side: 'ask' | 'bid',
    sizeUsd: number,
): number | null {
    if (!levels || levels.length === 0 || sizeUsd <= 0) return null;
    const sorted = [...levels].sort((a, b) => side === 'ask' ? a.price - b.price : b.price - a.price);
    const bestPrice = sorted[0].price;
    let remaining = sizeUsd;
    let totalCost = 0;
    let totalQty = 0;
    for (const { price, amount } of sorted) {
        const levelUsd = price * amount;
        if (remaining <= levelUsd) {
            totalCost += remaining;
            totalQty += remaining / price;
            break;
        }
        totalCost += levelUsd;
        totalQty += amount;
        remaining -= levelUsd;
    }
    if (totalQty === 0) return null;
    const avgPrice = totalCost / totalQty;
    return ((avgPrice - bestPrice) / bestPrice) * 100 * (side === 'bid' ? -1 : 1);
}

export function getFundingDirectionText(fundingRate: number, isLong: boolean): string {
    if (fundingRate > 0) {
        return isLong ? 'You pay' : 'You receive';
    }
    return isLong ? 'You receive' : 'You pay';
}
