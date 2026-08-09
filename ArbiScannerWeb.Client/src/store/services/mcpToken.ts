import { createApi } from '@reduxjs/toolkit/query/react';
import type { FluentResult } from '../../types/FluentResultType';
import { baseQueryWithReauth } from './baseQuery';

// Generates a personal, long-lived offline_access token (via a Standard Token
// Exchange server-side — see ArbiScannerWeb.Infrastructure's McpTokenService)
// that the user pastes into their own MCP client to reach ArbiScanner.McpServer
// as themselves, not a shared account.
export const mcpTokenApi = createApi({
    reducerPath: 'mcpTokenApi',
    baseQuery: baseQueryWithReauth,
    endpoints: (builder) => ({
        generateMcpToken: builder.mutation<FluentResult<string>, void>({
            query: () => ({
                url: '/McpToken/Generate',
                method: 'POST',
            }),
        }),
    }),
});

export const { useGenerateMcpTokenMutation } = mcpTokenApi;
