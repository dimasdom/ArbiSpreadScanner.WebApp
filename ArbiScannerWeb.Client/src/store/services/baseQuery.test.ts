import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { FetchBaseQueryArgs } from '@reduxjs/toolkit/query';

const rawBaseQueryMock = vi.fn();
const getAccessTokenMock = vi.fn();
let capturedOptions: FetchBaseQueryArgs | undefined;

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return {
        ...actual,
        fetchBaseQuery: (options: FetchBaseQueryArgs) => {
            capturedOptions = options;
            return rawBaseQueryMock;
        },
    };
});

vi.mock('../../services/oidcUserManager', () => ({
    getAccessToken: () => getAccessTokenMock(),
}));

describe('baseQueryWithReauth', () => {
    beforeEach(async () => {
        vi.resetModules();
        rawBaseQueryMock.mockReset();
        getAccessTokenMock.mockReset();
        capturedOptions = undefined;
        await import('./baseQuery');
    });

    it('passes through whatever fetchBaseQuery returns', async () => {
        const { baseQueryWithReauth } = await import('./baseQuery');
        expect(baseQueryWithReauth).toBe(rawBaseQueryMock);
    });

    it('attaches the Authorization header when a token is available', async () => {
        getAccessTokenMock.mockResolvedValue('the-token');

        const headers = await capturedOptions!.prepareHeaders!(new Headers(), {} as never);

        expect(headers.get('Authorization')).toBe('Bearer the-token');
    });

    it('does not attach an Authorization header when there is no token', async () => {
        getAccessTokenMock.mockResolvedValue(undefined);

        const headers = await capturedOptions!.prepareHeaders!(new Headers(), {} as never);

        expect(headers.get('Authorization')).toBeNull();
    });
});
