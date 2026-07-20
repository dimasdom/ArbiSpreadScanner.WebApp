import { describe, expect, it, vi } from 'vitest';

function mockLogger() {
    vi.doMock('./loggerService', () => ({
        logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn(), debug: vi.fn() },
    }));
}

describe('authTokenService', () => {
    it('warns and returns null before the access token getter is registered', async () => {
        vi.resetModules();
        mockLogger();
        const { logger } = await import('./loggerService');
        const { getAccessTokenValue } = await import('./authTokenService');

        const result = getAccessTokenValue();

        expect(result).toBeNull();
        expect(logger.warn).toHaveBeenCalledWith('Access token getter used before registration', 'authTokenService');
    });

    it('does not warn once the access token getter has been registered', async () => {
        vi.resetModules();
        mockLogger();
        const { logger } = await import('./loggerService');
        const { setTokenGetter, getAccessTokenValue } = await import('./authTokenService');
        setTokenGetter(() => 'token-abc');

        const result = getAccessTokenValue();

        expect(result).toBe('token-abc');
        expect(logger.warn).not.toHaveBeenCalled();
    });

    it('getToken delegates to getAccessTokenValue', async () => {
        vi.resetModules();
        mockLogger();
        const { setTokenGetter, getToken } = await import('./authTokenService');
        setTokenGetter(() => 'tok-xyz');

        expect(getToken()).toBe('tok-xyz');
    });

    it('warns and returns null before the refresh token getter is registered', async () => {
        vi.resetModules();
        mockLogger();
        const { logger } = await import('./loggerService');
        const { getRefreshTokenValue } = await import('./authTokenService');

        const result = getRefreshTokenValue();

        expect(result).toBeNull();
        expect(logger.warn).toHaveBeenCalledWith('Refresh token getter used before registration', 'authTokenService');
    });

    it('returns the refresh token once registered without warning', async () => {
        vi.resetModules();
        mockLogger();
        const { logger } = await import('./loggerService');
        const { setRefreshTokenGetter, getRefreshTokenValue } = await import('./authTokenService');
        setRefreshTokenGetter(() => 'refresh-abc');

        const result = getRefreshTokenValue();

        expect(result).toBe('refresh-abc');
        expect(logger.warn).not.toHaveBeenCalled();
    });

    it('keeps access and refresh token getters independent', async () => {
        vi.resetModules();
        mockLogger();
        const { setTokenGetter, getAccessTokenValue, getRefreshTokenValue } = await import('./authTokenService');
        setTokenGetter(() => 'access-only');

        expect(getAccessTokenValue()).toBe('access-only');
        expect(getRefreshTokenValue()).toBeNull();
    });
});
