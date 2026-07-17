export const Exchange = {
    Binance: "Binance",
    Bybit: "Bybit",
    OKX: "OKX",
    MEXC: "MEXC",
    Bitget: "Bitget",
    HTX: "HTX",
    XT: "XT",
    CoinEX: "CoinEX",
    LBank: "LBank",
    WhiteBit: "WhiteBit",
    KucoinFutures: "kucoinfutures"
} as const;

export type Exchange = typeof Exchange[keyof typeof Exchange];