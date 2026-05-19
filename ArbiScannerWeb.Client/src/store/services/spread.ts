import { createApi } from '@reduxjs/toolkit/query/react';
import type { TradeOpportunityDetailsDTO, TradeOpportunityModel } from '../../types/tradeOpportunityModel';
import type { FluentResult } from '../../types/FluentResultType';
import { baseQueryWithReauth } from './baseQuery';

export const spreadApi = createApi({
    reducerPath: 'spreadApi',
    baseQuery: baseQueryWithReauth,
    keepUnusedDataFor: 0,
    refetchOnMountOrArgChange: true,
    tagTypes: ['Spreads', 'Spread'],
    endpoints: (builder) => ({
        getSpreads: builder.query<FluentResult<TradeOpportunityModel[]>, void>({
            query: () => ({
                url: '/TradeOpportunity/GetSpreadsForUser',
                method: 'GET',
            }),
            transformResponse: (response: FluentResult<TradeOpportunityDetailsDTO[]>): FluentResult<TradeOpportunityModel[]> => ({
                ...response,
                value: (response.value || []).map((item) => item.positionModel),
            }),
            providesTags: ['Spreads'],
        }),
        getSpreadInfo: builder.query<FluentResult<TradeOpportunityDetailsDTO>, string>({
            query: (guid) => ({
                url: `/TradeOpportunity/GetSpreadInfo/${guid}`,
                method: 'GET',
            }),
            providesTags: (_result, _error, guid) => [{ type: 'Spread', id: guid }],
        }),
    }),
});

export const { useGetSpreadsQuery, useGetSpreadInfoQuery } = spreadApi;
