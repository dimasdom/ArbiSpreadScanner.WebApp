import { describe, expect, it, vi, type Mock } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useSubscriptionPage } from './useSubscriptionPage';

vi.mock('../store/services/subscription', async (importOriginal) => {
    const actual = await importOriginal<typeof import('../store/services/subscription')>();
    return { ...actual, useGetAllSubscriptionsQuery: vi.fn() };
});

import { useGetAllSubscriptionsQuery } from '../store/services/subscription';
const mockQuery = useGetAllSubscriptionsQuery as Mock;

describe('useSubscriptionPage', () => {
    it('unwraps the subscriptions list from the FluentResult envelope', () => {
        mockQuery.mockReturnValue({
            data: {
                isSuccess: true,
                isFailed: false,
                errors: [],
                reasons: [],
                value: [{ id: 1, type: 'Basic', price: 10, durationInDays: 30 }],
            },
            isLoading: false,
            error: undefined,
        });

        const { result } = renderHook(() => useSubscriptionPage());

        expect(result.current.subscriptions).toEqual([{ id: 1, type: 'Basic', price: 10, durationInDays: 30 }]);
        expect(result.current.isLoading).toBe(false);
        expect(result.current.error).toBeUndefined();
    });

    it('returns undefined subscriptions while data has not loaded yet', () => {
        mockQuery.mockReturnValue({ data: undefined, isLoading: true, error: undefined });

        const { result } = renderHook(() => useSubscriptionPage());

        expect(result.current.subscriptions).toBeUndefined();
        expect(result.current.isLoading).toBe(true);
    });

    it('surfaces query errors unchanged', () => {
        const error = { status: 500, data: 'server error' };
        mockQuery.mockReturnValue({ data: undefined, isLoading: false, error });

        const { result } = renderHook(() => useSubscriptionPage());

        expect(result.current.error).toEqual(error);
    });
});
