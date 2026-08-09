import { PositionAction } from "./PositionAction";
import { SpreadType } from "./SpreadType";
import type { PossiblePositionTickerModel } from "./tickerType";

export interface OrderBlock {
    price: number,
    amount:number
}

export interface ExchangeRateModel {
    id: number;
    symbol: string;
    exchange: string;
    exchangeRate: number;
    volumeAsk: number;
    volumeBid: number;
    slippageLong: number;
    slippageShort: number;
    summarySlipage: number;
    fundingRateValue?: number | null;
}

export interface TradeOpportunityModel {
    guid: string;
    exchangeRateA: ExchangeRateModel;
    exchangeRateB: ExchangeRateModel;
    exchangeShort: ExchangeRateModel;
    exchangeLong: ExchangeRateModel;
    summaryTarrif: number;
    possibleProfit: number;
    totalFunding: number;
    spread: number;
    type: SpreadType;
    actionType: PositionAction;
    startSpread: number;
    symbol: string;
    bidsExchangeA?: OrderBlock[] | null;
    asksExchangeA?: OrderBlock[] | null;
    bidsExchangeB?: OrderBlock[] | null;
    asksExchangeB?: OrderBlock[] | null;
    dateTime: string;
}

export interface SpreadAnalysisDTO {
    recommended: boolean,
    reasons: string[],
    trendWarning?: string | null,
}

export interface TradeOpportunityDetailsDTO {
    positionModel: TradeOpportunityModel,
    tickers: PossiblePositionTickerModel[],
    groupName: string,
    shortExchangeUrl?: string | null,
    longExchangeUrl?: string | null,
    /** Only populated for a single-spread lookup (get_spread_by_id) - null for list results. */
    analysis?: SpreadAnalysisDTO | null,
}

export interface RecommendedSpreadDTO {
    details: TradeOpportunityDetailsDTO,
    category: SpreadType,
}

export const isSpreadType = (value: unknown): value is SpreadType => {
    return typeof value === 'number' && value >= 0 && value <= 2;
};

export const isPositionAction = (value: unknown): value is PositionAction => {
    return typeof value === 'number' && value >= 0 && value <= 2;
};

export const isExchangeRateModel = (obj: unknown): obj is ExchangeRateModel => {
    const o = obj as Record<string, unknown>;
    return !!o &&
        typeof o.id === 'number' &&
        typeof o.symbol === 'string' &&
        typeof o.exchange === 'string' &&
        typeof o.exchangeRate === 'number' &&
        typeof o.volumeAsk === 'number' &&
        typeof o.volumeBid === 'number' &&
        typeof o.slippageLong === 'number' &&
        typeof o.slippageShort === 'number' &&
        typeof o.summarySlipage === 'number' &&
        (o.fundingRateValue === null || o.fundingRateValue === undefined || typeof o.fundingRateValue === 'number');
};

export const isPossiblePositionModel = (obj: unknown): obj is TradeOpportunityModel => {
    const o = obj as Record<string, unknown>;
    return !!o &&
        typeof o.guid === 'string' &&
        isExchangeRateModel(o.exchangeRateA) &&
        isExchangeRateModel(o.exchangeRateB) &&
        isExchangeRateModel(o.exchangeShort) &&
        isExchangeRateModel(o.exchangeLong) &&
        typeof o.summaryTarrif === 'number' &&
        typeof o.possibleProfit === 'number' &&
        typeof o.totalFunding === 'number' &&
        typeof o.spread === 'number' &&
        isSpreadType(o.type) &&
        isPositionAction(o.actionType) &&
        typeof o.startSpread === 'number' &&
        typeof o.symbol === 'string' &&
        typeof o.dateTime === 'string';
};

export const createEmptyExchangeRateModel = (): ExchangeRateModel => ({
    id: 0,
    symbol: '',
    exchange: '',
    exchangeRate: 0,
    volumeAsk: 0,
    volumeBid: 0,
    slippageLong: 0,
    slippageShort: 0,
    summarySlipage: 0,
    fundingRateValue: null
});

export const createEmptyPossiblePositionModel = (): TradeOpportunityModel => ({
    guid: '',
    exchangeRateA: createEmptyExchangeRateModel(),
    exchangeRateB: createEmptyExchangeRateModel(),
    exchangeShort: createEmptyExchangeRateModel(),
    exchangeLong: createEmptyExchangeRateModel(),
    summaryTarrif: 0,
    possibleProfit: 0,
    totalFunding: 0,
    spread: 0,
    type: SpreadType.Futures,
    actionType: PositionAction.Open,
    startSpread: 0,
    symbol: '',
    bidsExchangeA: null,
    asksExchangeA: null,
    bidsExchangeB: null,
    asksExchangeB: null,
    dateTime: new Date().toISOString()
});

export const formatDateTime = (date: Date): string => {
    return date.toISOString();
};

export const parseDateTime = (dateString: string): Date => {
    return new Date(dateString);
};

export interface ApiResponse<T> {
    data: T;
    success: boolean;
    message?: string;
    errors?: string[];
}

export interface PaginatedResponse<T> {
    data: T[];
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
}

