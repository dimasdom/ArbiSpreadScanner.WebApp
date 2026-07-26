import { describe, expect, it } from 'vitest';
import { normalizeApiError } from './normalizeApiError';

describe('normalizeApiError', () => {
    it('returns an UNKNOWN error when given undefined', () => {
        expect(normalizeApiError(undefined)).toEqual({
            code: 'UNKNOWN',
            message: 'Something went wrong. Please try again.',
        });
    });

    it('extracts code and message from a structured API error body', () => {
        const result = normalizeApiError({
            status: 400,
            data: { isSuccess: false, errorCode: 'VALIDATION', message: 'Bad input' },
        });

        expect(result).toEqual({ code: 'VALIDATION', message: 'Bad input' });
    });

    it.each(['FETCH_ERROR', 'TIMEOUT_ERROR', 'PARSING_ERROR'] as const)(
        'maps %s to a network error',
        (status) => {
            const result = normalizeApiError({ status, error: 'boom' } as never);

            expect(result).toEqual({
                code: 'NETWORK_ERROR',
                message: 'Unable to reach the server. Check your connection and try again.',
            });
        },
    );

    it('returns UNKNOWN for a FetchBaseQueryError without a structured body', () => {
        const result = normalizeApiError({ status: 500, data: 'Internal Server Error' });

        expect(result).toEqual({ code: 'UNKNOWN', message: 'Something went wrong. Please try again.' });
    });

    it('uses the SerializedError message for client-side errors', () => {
        const result = normalizeApiError({ name: 'Error', message: 'Something broke' });

        expect(result).toEqual({ code: 'CLIENT_ERROR', message: 'Something broke' });
    });

    it('falls back to a default message when a SerializedError has none', () => {
        const result = normalizeApiError({ name: 'Error' });

        expect(result).toEqual({ code: 'CLIENT_ERROR', message: 'Something went wrong. Please try again.' });
    });
});
