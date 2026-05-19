import type { OrderBlock, TradeOpportunityModel } from "./tradeOpportunityModel";

export interface PossiblePositionTickerModel {
    id: number;
    guid: string;
    symbol: string;
    spread: number;
    echangeA: string;
    exchangeB: string;
    exchangeLong: string;
    exchangeShort: string;
    rateA: number;
    rateB: number;
    dateTime: string; // ISO string format (e.g. "2025-07-01T12:00:00Z")

    bidsExchangeA?: OrderBlock[];
    asksExchangeA?: OrderBlock[];
    bidsExchangeB?: OrderBlock[];
    asksExchangeB?: OrderBlock[];
}
export interface MessageDTO {
    possiblePosition: TradeOpportunityModel,
    ticker: PossiblePositionTickerModel
}