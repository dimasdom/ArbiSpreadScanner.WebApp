import { createApi } from '@reduxjs/toolkit/query/react';
import type { FluentResult } from '../../types/FluentResultType';
import type { SnapshotIndexEntry, SpreadStatsSnapshot } from '../../types/StatsType';
import { baseQueryWithReauth } from './baseQuery';

export const statsApi = createApi({
    reducerPath: 'statsApi',
    baseQuery: baseQueryWithReauth,
    tagTypes: ['Stats'],
    endpoints: (builder) => ({
        getLatestStats: builder.query<FluentResult<SpreadStatsSnapshot>, void>({
            query: () => ({
                url: '/SpreadStats/GetLatest',
                method: 'GET',
            }),
            providesTags: ['Stats'],
        }),
        getStatsById: builder.query<FluentResult<SpreadStatsSnapshot>, string>({
            query: (id) => ({
                url: `/SpreadStats/GetById?id=${id}`,
                method: 'GET',
            }),
            providesTags: (_result, _error, id) => [{ type: 'Stats', id }],
        }),
        getSnapshotIndex: builder.query<FluentResult<SnapshotIndexEntry[]>, void>({
            query: () => ({
                url: '/SpreadStats/GetSnapshotIndex',
                method: 'GET',
            }),
            providesTags: ['Stats'],
        }),
    }),
});

export const { useGetLatestStatsQuery, useGetStatsByIdQuery, useGetSnapshotIndexQuery } = statsApi;
