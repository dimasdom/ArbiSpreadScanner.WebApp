import { fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import { getAccessToken } from '../../services/oidcUserManager';

const apiHost = import.meta.env.VITE_API_URL ?? '';
export const baseURL = apiHost ? `${apiHost}/api` : '/api';

// No more cookies or 401-triggered refresh dance — oidc-client-ts's
// automaticSilentRenew keeps the access token fresh in the background, and
// every request just attaches whatever token it currently holds.
export const baseQueryWithReauth = fetchBaseQuery({
    baseUrl: baseURL,
    prepareHeaders: async (headers) => {
        const token = await getAccessToken();
        if (token) {
            headers.set('Authorization', `Bearer ${token}`);
        }
        return headers;
    },
});
