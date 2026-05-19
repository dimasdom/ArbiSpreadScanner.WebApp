import { fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import type { BaseQueryFn, FetchArgs, FetchBaseQueryError } from '@reduxjs/toolkit/query';
import { coordinatedRefresh } from '../../services/refreshCoordinator';
import { logout } from '../slices/accountSlice';

const apiHost = import.meta.env.VITE_API_URL ?? '';
export const baseURL = apiHost ? `${apiHost}/api` : '/api';

const rawBaseQuery = fetchBaseQuery({
    baseUrl: baseURL,
    credentials: 'include',
});

const shouldSkipRefresh = (url: string) => {
    return [
        '/Account/Login',
        '/Account/Register',
        '/Account/ForgotPassword',
        '/Account/ResetPassword',
        '/Account/ConfirmEmail',
        '/Account/ResendEmailConfirmation',
        '/Account/Refresh',
    ].some((path) => url.includes(path));
};

const getRequestUrl = (args: string | FetchArgs) => (typeof args === 'string' ? args : args.url);

export const baseQueryWithReauth: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (
    args,
    api,
    extraOptions,
) => {
    let result = await rawBaseQuery(args, api, extraOptions);
    const requestUrl = getRequestUrl(args);

    if (
        result.error &&
        (result.error.status === 401 || result.error.status === 403) &&
        !shouldSkipRefresh(requestUrl)
    ) {
        try {
            await coordinatedRefresh(async () => {
                const refreshResult = await rawBaseQuery(
                    { url: '/Account/Refresh', method: 'POST' },
                    api,
                    extraOptions,
                );

                if (refreshResult.error) {
                    throw refreshResult.error;
                }

                return '';
            });

            result = await rawBaseQuery(args, api, extraOptions);
        } catch {
            api.dispatch(logout());
        }
    }

    if (result.error && (result.error.status === 401 || result.error.status === 403)) {
        api.dispatch(logout());
    }

    return result;
};
