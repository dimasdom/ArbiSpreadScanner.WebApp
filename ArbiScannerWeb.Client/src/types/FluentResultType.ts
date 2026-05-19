export interface FluentResult<T = unknown> {
    isSuccess: boolean;
    isFailed: boolean;
    errors: FluentError[];
    reasons: FluentReason[];
    value: T;
}

// Optional: FluentError and FluentReason types
export interface FluentError {
    message: string;
    metadata?: Record<string, unknown>;
}

export interface FluentReason {
    message: string;
    metadata?: Record<string, unknown>;
}