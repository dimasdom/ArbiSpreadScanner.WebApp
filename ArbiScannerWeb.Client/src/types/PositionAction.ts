
export const PositionAction = {
    Open: 0,
    Update: 1,
    Close: 2
} as const;

export type PositionAction = typeof PositionAction[keyof typeof PositionAction];
