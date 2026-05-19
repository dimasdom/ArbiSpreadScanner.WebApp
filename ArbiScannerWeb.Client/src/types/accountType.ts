// Telegram User Model
export interface ExchangeInfo {
    id: number;
    name: string;
}

export interface UserExchangeModel {
    id: number;
    userAccountId: string;
    exchangeId: number;
    exchange: ExchangeInfo;
}

export interface UserSettingsModel {
    id: number;
    userName?: string | null;
    chatId: number; // Note: JavaScript numbers can handle long values up to Number.MAX_SAFE_INTEGER
    spreadSize: number;
    positionSize: number;
    futuresSpread: boolean;
    fundingSpread: boolean;
    spotSpread: boolean;
    haveAccess: boolean;
    active: boolean;
    email?: string | null;
    exchanges: UserExchangeModel[];
}

export interface AccountUpdateDTO {
    userName?: string | null;
    spreadSize: number;
    positionSize: number;
    futuresSpread: boolean;
    fundingSpread: boolean;
    spotSpread: boolean;
    haveAccess: boolean;
    email?: string | null;
    exchanges: UserExchangeModel[];
}

// Account model intentionally contains only client-facing fields.
export interface AccountModel {
    id: string;
    userName?: string | null;
    email?: string | null;
    emailConfirmed: boolean;
    telegramUserId: number;
    userSettings: UserSettingsModel;
}

export interface AccountDTO extends AccountModel {
    emailConfirmToken: string;
}





// Utility functions for the new models
export const createEmptyUserSettingsModel = (): UserSettingsModel => ({
    id: 0,
    userName: null,
    chatId: 0,
    spreadSize: 0,
    positionSize: 0,
    futuresSpread: false,
    fundingSpread: false,
    spotSpread: false,
    haveAccess: false,
    active: false,
    email: null,
    exchanges: []
});

export const createEmptyAccountModel = (): AccountModel => ({
    id: '',
    userName: null,
    email: null,
    emailConfirmed: false,
    telegramUserId: 0,
    userSettings: createEmptyUserSettingsModel()
});

export const Exchange = {
    GateIo: "Gate.io",
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
    KucoinFutures: "KuCoin Futures",
    BingX: "BingX"
} as const;

export type Exchange = typeof Exchange[keyof typeof Exchange];

export interface UserSubscriptionModel {
  id: number;
  userId: string;
  subscriptionId: number;
  startDate: Date | string;
  endDate: Date | string;
  subscription?: SubscriptionModel | null;
}

export interface UserSubscriptionModelDTO extends UserSubscriptionModel {
    isActive: boolean;
}

// UserSubscriptionPayment
export interface UserSubscriptionPayment {
  id: number;
  userId: string;
  subscriptionId: number;
  paymentId: number;
  subscription?: SubscriptionModel | null;
  payment?: PaymentModel | null;
  expirationDate: Date | string;
}

export const PaymentStatus = {
  Pending: 0,
  Completed: 1,
  Failed: 2,
  Refunded: 3,
} as const;

export type PaymentStatus = typeof PaymentStatus[keyof typeof PaymentStatus];

// SubscriptionModel
export interface SubscriptionModel {
  id: number;
  type: string;
  price: number;
  durationInDays: number;
}

// PaymentModel
export interface PaymentModel {
  id: number;
  userId: string;
  amount: number;
  paymentDate: Date | string;
  paymentUrl: string;
  status: PaymentStatus;
  transactionId: string;
}

export interface EmailConfirmationCodes {
    id: number;
    email: string;
    code: string;
    userId: string;
    expirationTime: Date | string;
    accountId: string;
}

export interface TelegramLinkRequest {
    id: string;
    accountId: string;
}