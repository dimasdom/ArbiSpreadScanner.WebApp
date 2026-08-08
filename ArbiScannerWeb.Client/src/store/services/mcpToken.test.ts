import { beforeEach, describe, expect, it, vi } from 'vitest';
import { configureStore } from '@reduxjs/toolkit';

const rawBaseQueryMock = vi.fn();

vi.mock('@reduxjs/toolkit/query/react', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@reduxjs/toolkit/query/react')>();
    return { ...actual, fetchBaseQuery: () => rawBaseQueryMock };
});

async function buildStore() {
    const { mcpTokenApi } = await import('./mcpToken');
    return {
        mcpTokenApi,
        store: configureStore({
            reducer: { [mcpTokenApi.reducerPath]: mcpTokenApi.reducer },
            middleware: (getDefaultMiddleware) => getDefaultMiddleware().concat(mcpTokenApi.middleware),
        }),
    };
}

describe('mcpTokenApi', () => {
    beforeEach(() => {
        rawBaseQueryMock.mockReset();
        rawBaseQueryMock.mockResolvedValue({ data: { isSuccess: true, value: 'generated-token' } });
    });

    it('generateMcpToken posts to the expected URL', async () => {
        const { mcpTokenApi, store } = await buildStore();

        await store.dispatch(mcpTokenApi.endpoints.generateMcpToken.initiate());

        expect(rawBaseQueryMock.mock.calls[0][0]).toEqual({ url: '/McpToken/Generate', method: 'POST' });
    });

    it('generateMcpToken resolves with the generated token', async () => {
        const { mcpTokenApi, store } = await buildStore();

        const result = await store.dispatch(mcpTokenApi.endpoints.generateMcpToken.initiate()).unwrap();

        expect(result).toEqual({ isSuccess: true, value: 'generated-token' });
    });

    it('generateMcpToken surfaces a failed result without throwing', async () => {
        rawBaseQueryMock.mockResolvedValue({
            data: { isSuccess: false, value: null, errors: [{ message: 'No bearer token on the current request.' }] },
        });
        const { mcpTokenApi, store } = await buildStore();

        const result = await store.dispatch(mcpTokenApi.endpoints.generateMcpToken.initiate()).unwrap();

        expect(result.isSuccess).toBe(false);
        expect(result.errors[0].message).toBe('No bearer token on the current request.');
    });
});
