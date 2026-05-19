import { logger } from './loggerService';

let getAccessToken: () => string | null = () => null;
let getRefreshToken: () => string | null = () => null;
let isAccessGetterRegistered = false;
let isRefreshGetterRegistered = false;

export const setTokenGetter = (getter: () => string | null) => {
    getAccessToken = getter;
    isAccessGetterRegistered = true;
};

export const setRefreshTokenGetter = (getter: () => string | null) => {
    getRefreshToken = getter;
    isRefreshGetterRegistered = true;
};

export const getAccessTokenValue = (): string | null => {
    if (!isAccessGetterRegistered) {
        logger.warn('Access token getter used before registration', 'authTokenService');
    }
    return getAccessToken();
};

export const getToken = (): string | null => getAccessTokenValue();

export const getRefreshTokenValue = (): string | null => {
    if (!isRefreshGetterRegistered) {
        logger.warn('Refresh token getter used before registration', 'authTokenService');
    }
    return getRefreshToken();
};
