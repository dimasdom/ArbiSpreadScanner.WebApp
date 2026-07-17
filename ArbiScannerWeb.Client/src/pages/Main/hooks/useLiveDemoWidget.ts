import { useEffect, useRef, useState } from 'react';
import type { PossiblePositionTickerModel } from '../../../types/tickerType';
import type { OrderBlock as OrderBlockItem } from '../../../types/tradeOpportunityModel';

const TICK_INTERVAL_MS = 3000;
const MAX_TICKERS = 100;
const SEED_COUNT = 9;
const JITTER_PCT = 0.0005;
const BASE_RATE_A = 103;
const BASE_RATE_B = 100;
const SYMBOL = 'SOL/USDT';
const EXCHANGE_A = 'Binance';
const EXCHANGE_B = 'Bybit';
const OB_LEVELS = 5;
const OB_STEP_MIN_PCT = 0.0003;
const OB_STEP_MAX_PCT = 0.0008;
const OB_AMOUNT_MIN = 0.5;
const OB_AMOUNT_MAX = 15;

let idCounter = 1;

function randomBetween(min: number, max: number): number {
    return min + Math.random() * (max - min);
}

function jitterRate(rate: number): number {
    const changePct = (Math.random() * 2 - 1) * JITTER_PCT;
    return rate * (1 + changePct);
}

function generateOrderBookSide(midPrice: number, direction: 1 | -1): OrderBlockItem[] {
    const levels: OrderBlockItem[] = [];
    let cumulativeOffsetPct = 0;
    for (let i = 0; i < OB_LEVELS; i++) {
        cumulativeOffsetPct += randomBetween(OB_STEP_MIN_PCT, OB_STEP_MAX_PCT);
        const price = midPrice * (1 + direction * cumulativeOffsetPct);
        const amount = Number(randomBetween(OB_AMOUNT_MIN, OB_AMOUNT_MAX).toFixed(3));
        levels.push({ price: Number(price.toFixed(4)), amount });
    }
    return levels;
}

function buildTicker(prevRateA: number, prevRateB: number, dateTime: Date) {
    const rateA = jitterRate(prevRateA);
    const rateB = jitterRate(prevRateB);
    const spread = ((rateA - rateB) / rateB) * 100;
    const ticker: PossiblePositionTickerModel = {
        id: idCounter,
        guid: `demo-${idCounter++}`,
        symbol: SYMBOL,
        spread,
        echangeA: EXCHANGE_A,
        exchangeB: EXCHANGE_B,
        exchangeLong: EXCHANGE_B,
        exchangeShort: EXCHANGE_A,
        rateA: Number(rateA.toFixed(4)),
        rateB: Number(rateB.toFixed(4)),
        dateTime: dateTime.toISOString(),
        asksExchangeA: generateOrderBookSide(rateA, 1),
        bidsExchangeA: generateOrderBookSide(rateA, -1),
        asksExchangeB: generateOrderBookSide(rateB, 1),
        bidsExchangeB: generateOrderBookSide(rateB, -1),
    };
    return { ticker, rateA, rateB };
}

function buildSeed() {
    const now = Date.now();

    const baseline: PossiblePositionTickerModel = {
        id: 0,
        guid: 'demo-seed-0',
        symbol: SYMBOL,
        spread: ((BASE_RATE_A - BASE_RATE_B) / BASE_RATE_B) * 100,
        echangeA: EXCHANGE_A,
        exchangeB: EXCHANGE_B,
        exchangeLong: EXCHANGE_B,
        exchangeShort: EXCHANGE_A,
        rateA: BASE_RATE_A,
        rateB: BASE_RATE_B,
        dateTime: new Date(now - SEED_COUNT * TICK_INTERVAL_MS).toISOString(),
        asksExchangeA: generateOrderBookSide(BASE_RATE_A, 1),
        bidsExchangeA: generateOrderBookSide(BASE_RATE_A, -1),
        asksExchangeB: generateOrderBookSide(BASE_RATE_B, 1),
        bidsExchangeB: generateOrderBookSide(BASE_RATE_B, -1),
    };

    const tickers: PossiblePositionTickerModel[] = [baseline];
    let rateA = BASE_RATE_A;
    let rateB = BASE_RATE_B;

    for (let i = 1; i <= SEED_COUNT; i++) {
        const dateTime = new Date(now - (SEED_COUNT - i) * TICK_INTERVAL_MS);
        const step = buildTicker(rateA, rateB, dateTime);
        tickers.push(step.ticker);
        rateA = step.rateA;
        rateB = step.rateB;
    }

    return { tickers, rateA, rateB };
}

export function useLiveDemoWidget() {
    const [seed] = useState(() => buildSeed());
    const [tickers, setTickers] = useState(seed.tickers);
    const rateRef = useRef({ rateA: seed.rateA, rateB: seed.rateB });

    useEffect(() => {
        const intervalId = setInterval(() => {
            const { ticker, rateA, rateB } = buildTicker(rateRef.current.rateA, rateRef.current.rateB, new Date());
            rateRef.current = { rateA, rateB };
            setTickers((prev) => {
                const next = [...prev, ticker];
                return next.length > MAX_TICKERS ? next.slice(next.length - MAX_TICKERS) : next;
            });
        }, TICK_INTERVAL_MS);
        return () => clearInterval(intervalId);
    }, []);

    const last = tickers[tickers.length - 1];

    return {
        tickers,
        symbol: SYMBOL,
        exchangeA: EXCHANGE_A,
        exchangeB: EXCHANGE_B,
        spreadVal: last ? Math.abs(last.spread) : 0,
        asksA: last?.asksExchangeA,
        bidsA: last?.bidsExchangeA,
        asksB: last?.asksExchangeB,
        bidsB: last?.bidsExchangeB,
    };
}
