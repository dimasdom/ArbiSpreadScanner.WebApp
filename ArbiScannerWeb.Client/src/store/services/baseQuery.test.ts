import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { BaseQueryApi } from '@reduxjs/toolkit/query';
import { logout } from '../slices/accountSlice';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

function buildApi(): BaseQueryApi {
    return {
        signal: new AbortController().signal,
        dispatch: vi.fn(),
        getState: () => ({}),
        extra: undefined,
        endpoint: 'test',
        type: 'query',
    } as unknown as BaseQueryApi;
}

describe('baseQueryWithReauth', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
    });

    it('passes through a successful response unchanged', async () => {
        const { baseQueryWithReauth } = await import('./baseQuery');
        rawBaseQueryMock.mockResolvedValueOnce({ data: { ok: true } });
        const api = buildApi();

        const result = await baseQueryWithReauth('/Spread/Get', api, {});

        expect(result).toEqual({ data: { ok: true } });
        expect(api.dispatch).not.toHaveBeenCalled();
    });

    it('does not attempt a refresh for the login endpoint on 401', async () => {
        const { baseQueryWithReauth } = await import('./baseQuery');
        rawBaseQueryMock.mockResolvedValueOnce({ error: { status: 401 } });
        const api = buildApi();

        const result = await baseQueryWithReauth({ url: '/Account/Login' }, api, {});

        expect(result).toEqual({ error: { status: 401 } });
        expect(api.dispatch).toHaveBeenCalledWith(logout());
        expect(rawBaseQueryMock).toHaveBeenCalledTimes(1);
    });

    it('refreshes the token and retries once on 401, then succeeds', async () => {
        const { baseQueryWithReauth } = await import('./baseQuery');
        rawBaseQueryMock
            .mockResolvedValueOnce({ error: { status: 401 } })
            .mockResolvedValueOnce({ data: {} })
            .mockResolvedValueOnce({ data: { retried: true } });
        const api = buildApi();

        const result = await baseQueryWithReauth('/Spread/Get', api, {});

        expect(result).toEqual({ data: { retried: true } });
        expect(rawBaseQueryMock).toHaveBeenCalledTimes(3);
        expect(rawBaseQueryMock.mock.calls[1][0]).toEqual({ url: '/Account/Refresh', method: 'POST' });
        expect(api.dispatch).not.toHaveBeenCalled();
    });

    it('logs out when the refresh call itself fails', async () => {
        const { baseQueryWithReauth } = await import('./baseQuery');
        rawBaseQueryMock
            .mockResolvedValueOnce({ error: { status: 401 } })
            .mockResolvedValueOnce({ error: { status: 401, data: 'refresh failed' } });
        const api = buildApi();

        const result = await baseQueryWithReauth('/Spread/Get', api, {});

        expect(result).toEqual({ error: { status: 401 } });
        expect(api.dispatch).toHaveBeenCalledWith(logout());
        expect(rawBaseQueryMock).toHaveBeenCalledTimes(2);
    });

    it('logs out on a 403 without attempting a refresh retry loop twice', async () => {
        const { baseQueryWithReauth } = await import('./baseQuery');
        rawBaseQueryMock
            .mockResolvedValueOnce({ error: { status: 403 } })
            .mockResolvedValueOnce({ data: {} })
            .mockResolvedValueOnce({ error: { status: 403 } });
        const api = buildApi();

        const result = await baseQueryWithReauth('/Spread/Get', api, {});

        expect(result).toEqual({ error: { status: 403 } });
        expect(api.dispatch).toHaveBeenCalledWith(logout());
    });

    it('leaves non-auth errors untouched', async () => {
        const { baseQueryWithReauth } = await import('./baseQuery');
        rawBaseQueryMock.mockResolvedValueOnce({ error: { status: 500 } });
        const api = buildApi();

        const result = await baseQueryWithReauth('/Spread/Get', api, {});

        expect(result).toEqual({ error: { status: 500 } });
        expect(api.dispatch).not.toHaveBeenCalled();
        expect(rawBaseQueryMock).toHaveBeenCalledTimes(1);
    });
});
