import React, { useEffect, useRef } from 'react';
import type { OrderBlock as OrderBlockItem } from '../../types/tradeOpportunityModel';

interface OrderBlockProps {
    title: string | undefined | null;
    bids?: OrderBlockItem[];
    asks?: OrderBlockItem[];
}

const OrderBlock: React.FC<OrderBlockProps> = ({ title, bids = [], asks = [] }) => {
    // Asks: highest price at top, lowest (best) at bottom → sort descending, auto-scroll pins best at bottom
    const sortedAsks = [...asks].sort((a, b) => b.price - a.price);
    // Bids: highest price at top (nearest spread) → sort descending, render top→bottom
    const sortedBids = [...bids].sort((a, b) => b.price - a.price);

    const bestAsk = sortedAsks[sortedAsks.length - 1]?.price;
    const bestBid = sortedBids[0]?.price;

    const asksRef = useRef<HTMLDivElement>(null);

    // Keep asks scrolled to bottom so lowest ask (nearest spread) is always visible
    useEffect(() => {
        if (asksRef.current) {
            asksRef.current.scrollTop = asksRef.current.scrollHeight;
        }
    }, [asks]);

    const renderRow = (side: 'buy' | 'sell', price: number, amount: number, i: number) => {
        const rowClass = side === 'buy'
            ? 'bg-green-50 dark:bg-green-900/20 text-green-900 dark:text-green-300'
            : 'bg-red-50 dark:bg-red-900/20 text-red-900 dark:text-red-300';
        const sumUsd = price * amount;

        const bestPrice = side === 'sell' ? bestAsk : bestBid;
        const diffPct = bestPrice != null && bestPrice !== 0
            ? ((Math.abs(price - bestPrice) / bestPrice) * 100)
            : null;

        return (
            <div
                key={`${side}-${i}`}
                className={`flex justify-between px-4 py-2 ${rowClass} text-sm rounded mb-1`}
            >
                <span className="w-1/4 capitalize font-medium">{side}</span>
                <span className="w-1/4 text-right">${sumUsd.toFixed(2)}</span>
                <span className="w-1/4 text-right">{price.toFixed(4)}</span>
                <span className="w-1/4 text-right opacity-60 text-xs">
                    {diffPct != null ? (diffPct === 0 ? 'best' : `+${diffPct.toFixed(2)}%`) : '—'}
                </span>
            </div>
        );
    };

    const ROW_HEIGHT = 41;
    const VISIBLE_ROWS = 5;
    const sectionHeight = ROW_HEIGHT * VISIBLE_ROWS;

    return (
        <div className='flex justify-center'>
            <div className="border border-gray-200 dark:border-gray-700 rounded-3xl p-4 w-full max-w-sm bg-white dark:bg-gray-800 shadow-md transition-colors duration-200">
                <h3 className="text-lg font-bold mb-4 text-gray-900 dark:text-gray-100">
                    {title}
                </h3>
                <div className="flex justify-between px-4 py-1 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide mb-1">
                    <span className="w-1/4">Side</span>
                    <span className="w-1/4 text-right">Sum ($)</span>
                    <span className="w-1/4 text-right">Price</span>
                    <span className="w-1/4 text-right">Diff</span>
                </div>

                {/* Asks — scroll up to see higher prices, lowest ask pinned at bottom */}
                <div
                    ref={asksRef}
                    style={{ height: sectionHeight }}
                    className="overflow-y-auto"
                >
                    {sortedAsks.map(({ price, amount }, i) => renderRow('sell', price, amount, i))}
                </div>
                <div
                    style={{ height: sectionHeight }}
                    className="overflow-y-auto"
                >
                    {sortedBids.map(({ price, amount }, i) => renderRow('buy', price, amount, i))}
                </div>
            </div>
        </div>
    );
};

export default OrderBlock;
