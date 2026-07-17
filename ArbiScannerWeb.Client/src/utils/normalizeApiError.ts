import type { FetchBaseQueryError } from '@reduxjs/toolkit/query';
import type { SerializedError } from '@reduxjs/toolkit';
import type { ApiErrorBody } from '../types/ApiError';

export interface NormalizedApiError {
    code: string;
    message: string;
}

const isApiErrorBody = (data: unknown): data is ApiErrorBody => {
    return (
        typeof data === 'object' &&
        data !== null &&
        'errorCode' in data &&
        'message' in data
    );
};

export const normalizeApiError = (
    error: FetchBaseQueryError | SerializedError | undefined,
): NormalizedApiError => {
    if (!error) {
        return { code: 'UNKNOWN', message: 'Something went wrong. Please try again.' };
    }

    if ('status' in error) {
        if (isApiErrorBody(error.data)) {
            return { code: error.data.errorCode, message: error.data.message };
        }

        if (error.status === 'FETCH_ERROR' || error.status === 'TIMEOUT_ERROR' || error.status === 'PARSING_ERROR') {
            return { code: 'NETWORK_ERROR', message: 'Unable to reach the server. Check your connection and try again.' };
        }

        return { code: 'UNKNOWN', message: 'Something went wrong. Please try again.' };
    }

    return { code: 'CLIENT_ERROR', message: error.message ?? 'Something went wrong. Please try again.' };
};
