import { SpreadType } from './SpreadType';

export interface SymbolAverageSpreadEntry {
    symbol: string;
    averageSpreadPercent: number;
    sampleCount: number;
}

export interface ExchangeCountEntry {
    exchange: string;
    count: number;
}

export interface ExchangePairCountEntry {
    exchangeA: string;
    exchangeB: string;
    count: number;
}

export interface ExchangeMedianVolumeEntry {
    exchange: string;
    medianVolume: number;
    sampleCount: number;
}

export interface SpreadTypeCountEntry {
    type: SpreadType;
    count: number;
}

export interface SymbolCountEntry {
    symbol: string;
    count: number;
}

export interface SpreadStatsSnapshot {
    id: string;
    generatedAtUtc: string;
    totalSpreadsAnalyzed: number;
    topSymbolsByAverageSpread: SymbolAverageSpreadEntry[];
    topExchangesByCount: ExchangeCountEntry[];
    topExchangePairsByCount: ExchangePairCountEntry[];
    medianVolumeByExchange: ExchangeMedianVolumeEntry[];
    spreadTypeDistribution: SpreadTypeCountEntry[];
    topSymbolsByCount: SymbolCountEntry[];
}

export interface SnapshotIndexEntry {
    id: string;
    generatedAtUtc: string;
}
