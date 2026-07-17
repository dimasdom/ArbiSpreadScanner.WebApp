// Enums

export const SpreadType = {
    Futures: 0,
    Funding: 1,
    Spot: 2
} as const;

export type SpreadType = typeof SpreadType[keyof typeof SpreadType];

export const SpreadTypeNames: Record<SpreadType, keyof typeof SpreadType> = {
    [SpreadType.Futures]: 'Futures',
    [SpreadType.Funding]: 'Funding',
    [SpreadType.Spot]: 'Spot',
};
