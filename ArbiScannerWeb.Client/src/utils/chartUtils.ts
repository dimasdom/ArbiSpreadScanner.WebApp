import type { PossiblePositionTickerModel } from '../types/tickerType';

export function convertToOHLC(tickers: PossiblePositionTickerModel[]) {
    if (!tickers || tickers.length === 0) return [];

    const grouped = new Map<string, PossiblePositionTickerModel[]>();

    for (const tick of tickers) {
        const dt = new Date(tick.dateTime);
        const key = `${dt.getFullYear()}-${dt.getMonth()}-${dt.getDate()}-${dt.getHours()}-${dt.getMinutes()}`;

        if (!grouped.has(key)) {
            grouped.set(key, []);
        }
        grouped.get(key)!.push(tick);
    }

    const candles = Array.from(grouped.entries()).map(([, group]) => {
        const sorted = group.sort((a, b) => new Date(a.dateTime).getTime() - new Date(b.dateTime).getTime());
        const open = sorted[0].spread;
        const close = sorted[sorted.length - 1].spread;
        const high = Math.max(...sorted.map((t) => t.spread));
        const low = Math.min(...sorted.map((t) => t.spread));
        const x = new Date(sorted[0].dateTime);

        return { x, y: [open, high, low, close] };
    });

    candles.sort((a, b) => a.x.getTime() - b.x.getTime());

    return candles;
}

export function convertToSeries(tickers: PossiblePositionTickerModel[]) {
    return tickers
        .map((tick) => ({
            x: new Date(tick.dateTime).getTime(),
            y: tick.spread,
        }))
        .filter((point) => !isNaN(point.x) && typeof point.y === 'number')
        .sort((a, b) => a.x - b.x);
}
